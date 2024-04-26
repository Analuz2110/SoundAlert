
#include <Wire.h>
#include <WiFi.h>
#include <PubSubClient.h>

#define alimentacao 5
#define sensor 34 

// Configurações do LED RGB
#define red 21  // Pino do canal vermelho
#define green 19   // Pino do canal verde
#define blue 18  // Pino do canal azul

const unsigned long duration = 60000;  // 60 seconds in milliseconds
unsigned long startTime;

// WiFi
const char *ssid = "iPhone Luz"; // Enter your WiFi name
const char *password = "analuz2110";  // Enter WiFi password

// MQTT Broker
const char *mqtt_broker = "broker.emqx.io"; // broker address
const char *topic = "ufabc/soundalert"; // define topic 
const char *mqtt_username = "emqx"; // username for authentication
const char *mqtt_password = "public"; // password for authentication
const int mqtt_port = 1883; // port of MQTT over TCP

WiFiClient espClient;
PubSubClient client(espClient);

void setup() {
  // Set the GPIO pin to OUTPUT mode
  pinMode(alimentacao, OUTPUT);
  //pinMode(sensor, INPUT);
  pinMode(red, OUTPUT);//Define a variável azul como saída
  pinMode(green, OUTPUT);//Define a variável verde como saída
  pinMode (blue, OUTPUT);//Define a variável vermelho como saída

  // Turn on the GPIO pin by setting it to HIGH
  digitalWrite(alimentacao, HIGH);
  acenderLedVermelho();

  delay(500);

  Serial.begin(115200);
  startTime = millis();

  while (!Serial)
    delay(10); // will pause Zero, Leonardo, etc until serial console opens

  setup_wifi();

  // publish and subscribe
  client.publish(topic, "soundalert"); // publish to the topic
  client.subscribe(topic); // subscribe from the topic
}

void loop() {

  int sensorValue = analogRead(sensor); // Replace A0 with your ADC pin (e.g., ADC1_CH0 or ADC2_CH0)
  //Serial.println(sensorValue);

  String result = String(sensorValue);
  Serial.println(result);
  //char payload[10]; // Buffer to hold the sensor value as a string
  //snprintf(payload, sizeof(payload), "%d", sensorValue); // Convert int to string

  //Serial.println(payload);
    // Publish sensor value to MQTT topic
  client.publish(topic, result.c_str());

  delay(1000); // Adjust delay as needed

  if (WiFi.status() == WL_CONNECTED){
    // Esperar por uma mensagem
    if (client.loop() && client.connected()) {
      acenderLedVerde();
    } else {
      acenderLedAzul();
      setup_broker();
    }
  } else{
    acenderLedVermelho();
    setup_wifi();
  }

  client.loop();
  delay(500);  

}

void setup_wifi() {
  Serial.println();
  Serial.print("Connecting to ");
  Serial.println("\"" + String(ssid) + "\"");

  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi connected");
  Serial.print("IP address: ");
  Serial.println(WiFi.localIP());

  acenderLedAzul();
  setup_broker();
}

void setup_broker(){
  //connecting to a mqtt broker
  client.setServer(mqtt_broker, mqtt_port);
  client.setCallback(callback);
  while (!client.connected()) {
      String client_id = "esp8266-client-";
      client_id += String(WiFi.macAddress());
      Serial.printf("The client %s connects to the public mqtt broker\n", client_id.c_str());
      if (client.connect(client_id.c_str(), mqtt_username, mqtt_password)) {
          Serial.println("Public emqx mqtt broker connected");
      } else {
          Serial.print("failed with state ");
          Serial.print(client.state());
          delay(2000);
      }
  }

  acenderLedVerde();
}

void callback(char *topic, byte *payload, unsigned int length) {
    Serial.print("Message arrived in topic: ");
    Serial.println(topic);
    Serial.print("Message:");
    for (int i = 0; i < length; i++) {
        Serial.print((char) payload[i]);
    }
    Serial.println();
    Serial.println("-----------------------");
}

void acenderLedAzul() {
  digitalWrite(blue, HIGH);
  digitalWrite(green, LOW);
  digitalWrite(red, LOW);
}

void acenderLedVermelho() {
  digitalWrite(blue, LOW);
  digitalWrite(green, LOW);
  digitalWrite(red, HIGH);
}

void acenderLedVerde() {
  digitalWrite(blue, LOW);
  digitalWrite(green, HIGH);
  digitalWrite(red, LOW);
}
