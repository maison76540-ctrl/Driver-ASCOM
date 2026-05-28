// ArduSafeMon v1.2
// Pin 8 : HIGH = toit ouvert = safe | LOW = toit fermé = unsafe
// Mode push : envoie l'état toutes les 500 ms sans attendre de commande.
// Répond aussi aux commandes "S#" pour compatibilité avec l'ancien driver ASCOM.

void setup() {
  pinMode(8, INPUT);
  Serial.begin(9600);
}

void loop() {
  // Envoi continu toutes les 500 ms
  bool safe = (digitalRead(8) == HIGH);
  Serial.print(safe ? "safe#" : "notsafe#");

  // Pendant le délai, répondre aux commandes S# (ancien driver ASCOM)
  unsigned long t = millis();
  while (millis() - t < 500) {
    if (Serial.available() > 0) {
      String cmd = Serial.readStringUntil('#');
      if (cmd == "S") {
        safe = (digitalRead(8) == HIGH);
        Serial.print(safe ? "safe#" : "notsafe#");
      }
    }
  }
}
