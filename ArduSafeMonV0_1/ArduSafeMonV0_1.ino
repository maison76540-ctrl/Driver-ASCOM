// ArduSafeMon v1.0
// Pin 8 : HIGH = toit ouvert = safe | LOW = toit fermé = unsafe
// Protocole : reçoit "S#" → répond "safe#" ou "notsafe#"

bool pinState = false;

void setup() {
  pinMode(8, INPUT);
  Serial.begin(9600);
  Serial.flush();
  Serial.print("notsafe#");  // état initial sécuritaire au démarrage
}

void loop() {
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('#');
    if (cmd == "S") {
      pinState = digitalRead(8);
      if (pinState == HIGH) {
        Serial.print("safe#");     // toit ouvert → observation possible
      } else {
        Serial.print("notsafe#");  // toit fermé → observation impossible
      }
    }
  }
}
