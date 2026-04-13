bool pinState = 0;

//-----------------------------------------------------------------------------------------
//-----------------------------------------------------------------------------------------
//-----------------------------------------------------------------------------------------

void setup() {

  pinMode(8, INPUT);

  Serial.begin(9600);        // initialize serial
  Serial.flush();            // flush the port
  Serial.print("notsafe#");  // send notsafe# as first state while monitor and client initialize
}

void loop() {

  String cmd;

  if (Serial.available() > 0) {
    cmd = Serial.readStringUntil('#');
    if (cmd == "S") {
      pinState = digitalRead(8);
      if (pinState > 0.5) {
        Serial.print("safe#");
      }
      if (pinState < 0.5) {
        Serial.print("notsafe#");
      }
      delay(10000);
    }
  }
}
