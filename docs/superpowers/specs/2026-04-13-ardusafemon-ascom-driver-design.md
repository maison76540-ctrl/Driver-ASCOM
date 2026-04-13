# Design Spec — ArduSafeMon ASCOM Driver v1.0

**Date :** 2026-04-13  
**Auteur :** Brainstorming session  
**Statut :** Approuvé

---

## Contexte et problème

Le driver ASCOM existant `ArduSafeMon` (Safety Monitor pour capteur de position de toit) est devenu instable après une mise à jour de NINA. Le toit passe de manière erratique entre l'état `safe` et `unsafe`.

**Cause racine identifiée :** Le firmware Arduino contient un `delay(10000)` bloquant de 10 secondes après chaque réponse. Si NINA interroge le driver plus fréquemment que toutes les 10 secondes, les commandes `S#` s'accumulent dans le buffer série. À la fin du délai, l'Arduino traite plusieurs commandes en rafale, produisant des réponses incohérentes que le driver interprète comme des oscillations d'état.

**Matériel :**
- Arduino Uno connecté en USB/série
- Pin 8 : signal numérique entrant
  - `HIGH (1)` = toit ouvert → **safe** (on peut observer)
  - `LOW (0)` = toit fermé → **unsafe** (équipement protégé, observation impossible)

**Environnement cible :**
- ASCOM Platform 6.2
- NINA (dernière version)
- Windows 10/11
- .NET Framework 4.6.2

---

## Architecture générale

```
┌─────────────────────────────────────────────────────┐
│                    PC (Windows)                     │
│                                                     │
│   NINA                                              │
│    │ appelle IsSafe (fréquence variable)            │
│    ▼                                                │
│   ASCOM Driver (COM object C#)                      │
│    ├── Propriété IsSafe → lit le cache (_isSafe)    │
│    └── Thread polling (toutes les 2s)               │
│         │ envoie "S#"                               │
│         │ lit "safe#" ou "notsafe#"                 │
│         ▼                                           │
│        Port série (COM x, 9600 baud)                │
└──────────────────┬──────────────────────────────────┘
                   │ USB/Série
┌──────────────────▼──────────────────────────────────┐
│              Arduino Uno                            │
│   loop() : attend "S#" → lit pin 8 → répond        │
│   pin 8 = HIGH → "safe#"   (toit ouvert)           │
│   pin 8 = LOW  → "notsafe#" (toit fermé)           │
└─────────────────────────────────────────────────────┘
```

**Principe clé :** Le thread de polling isole complètement NINA du port série. NINA lit toujours une valeur en cache — stable, instantanée, sans contention.

---

## Composant 1 — Firmware Arduino (mis à jour)

### Changements par rapport à v0.1
- Suppression du `delay(10000)` bloquant
- Utilisation de `HIGH`/`LOW` au lieu de comparaisons flottantes

### Code complet

```cpp
bool pinState = false;

void setup() {
  pinMode(8, INPUT);
  Serial.begin(9600);
  Serial.flush();
  Serial.print("notsafe#");  // état initial sécuritaire
}

void loop() {
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('#');
    if (cmd == "S") {
      pinState = digitalRead(8);
      if (pinState == HIGH) {
        Serial.print("safe#");    // pin 8 = 1 → toit ouvert → safe
      } else {
        Serial.print("notsafe#"); // pin 8 = 0 → toit fermé → unsafe
      }
    }
  }
}
```

### Protocole série
| Direction | Message | Signification |
|---|---|---|
| PC → Arduino | `S#` | Demande d'état |
| Arduino → PC | `safe#` | Toit ouvert, observation possible |
| Arduino → PC | `notsafe#` | Toit fermé, observation impossible |

---

## Composant 2 — Driver ASCOM C#

### Structure des fichiers

```
ArduSafeMon/
├── ArduSafeMon.sln
└── ArduSafeMon/
    ├── ArduSafeMon.csproj        (.NET 4.6.2, COM-visible)
    ├── Driver.cs                  (classe principale ASCOM)
    ├── SetupDialogForm.cs         (fenêtre de configuration)
    ├── SetupDialogForm.Designer.cs
    └── Properties/
        └── AssemblyInfo.cs
```

### Interface ASCOM implémentée : `ISafetyMonitor` (v2)

| Propriété / Méthode | Type | Comportement |
|---|---|---|
| `Connected` | `bool` (get/set) | `true` : ouvre le port série et démarre le thread de polling. `false` : arrête le thread et ferme le port. |
| `IsSafe` | `bool` (get) | Retourne `_isSafe` depuis le cache thread-safe. |
| `Name` | `string` | `"ArduSafeMon"` |
| `Description` | `string` | `"Arduino Roof Safety Monitor"` |
| `DriverInfo` | `string` | `"ArduSafeMon v1.0 - ASCOM Safety Monitor"` |
| `DriverVersion` | `string` | `"1.0"` |
| `InterfaceVersion` | `short` | `2` |
| `SetupDialog()` | `void` | Ouvre `SetupDialogForm` |
| `Dispose()` | `void` | Déconnecte proprement |

### Identifiants COM
- **ProgID :** `ASCOM.ArduSafeMon.SafetyMonitor`
- **Description :** `ArduSafeMon Safety Monitor`

### Paramètres configurables (SetupDialogForm)
- **Port COM** : liste déroulante des ports disponibles sur le système
- **Intervalle de polling** : 2000 ms par défaut (modifiable 500–10000 ms)
- Stockage : registre Windows via `ASCOM.Utilities.Profile` (standard ASCOM)

---

## Composant 3 — Thread de polling & gestion d'état

### Flux de données

```
Thread principal (NINA)          Thread de polling (arrière-plan)
─────────────────────────        ──────────────────────────────────
Connect()                  →     Démarre le thread
                                  │
IsSafe  ──────────────────────→  lit _isSafe (lock)   ← toutes les 2s
IsSafe  ──────────────────────→  lit _isSafe (lock)        │
IsSafe  ──────────────────────→  lit _isSafe (lock)        │ envoie "S#"
                                                            │ lit réponse
                                                            │ met à jour _isSafe
Disconnect()               →     Arrête le thread (CancellationToken)
                                  Ferme le port série
```

### Sécurité thread (thread-safe)

```csharp
private bool _isSafe = false;
private readonly object _lock = new object();

// Dans le thread de polling :
lock (_lock) { _isSafe = nouvelleValeur; }

// Dans la propriété IsSafe :
lock (_lock) { return _isSafe; }
```

### Gestion des erreurs série

| Situation | Comportement |
|---|---|
| Timeout de lecture (>1000 ms) | Conserve l'état précédent, log l'avertissement |
| Port série perdu / déconnexion | `_isSafe = false` (sécurité par défaut), tentative de reconnexion toutes les 5s |
| Réponse inattendue / corrompue | Ignore la réponse, conserve l'état précédent |
| Exception série générale | `_isSafe = false`, log l'erreur |

**Règle de sécurité :** En cas de doute, l'état est toujours `false` (unsafe). NINA ne prend pas de risque avec l'équipement.

---

## Gestion d'erreurs globale

- Toutes les exceptions publiques ASCOM sont de type `ASCOM.DriverException`
- Les erreurs internes sont loggées via `ASCOM.Utilities.TraceLogger`
- Le driver ne plante jamais NINA : toute exception non gérée retourne `unsafe`

---

## Tests à effectuer

1. **Test de base** : Connexion → lecture `IsSafe` → vérifier cohérence avec position physique du toit
2. **Test de stabilité** : Laisser tourner 10 minutes → pas d'oscillations erratiques
3. **Test de fréquence** : Forcer des appels `IsSafe` rapides (10/s) → état stable
4. **Test de déconnexion** : Débrancher l'Arduino → driver passe en `unsafe`, reconnecte automatiquement
5. **Test NINA** : Intégration complète dans NINA Safety Monitor → état affiché stable
