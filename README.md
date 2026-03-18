# IoT Monitoring System - Kvalita vzduchu a vetranie

IoT riešenie pre monitorovanie kvality vzduchu v miestnosti s automatickým odporúčaním vetrania. Systém využíva Raspberry Pi so senzormi na snímanie koncentrácie CO2 a detekciu otvoreného okna.

## O projekte

Školský IoT projekt zameraný na praktické využitie senzorov s Raspberry Pi. Systém v reálnom čase monitoruje kvalitu vzduchu v miestnosti a na základe nameraných hodnôt odporúča používateľovi kedy vetrať.

Senzory snímajú:
- Koncentráciu CO2/kyslíka v miestnosti
- Stav okna (otvorené/zatvorené)

Na základe týchto dát systém vyhodnocuje kvalitu vzduchu a zobrazuje odporúčania na webovom dashboarde. Pri kritických hodnotách odosiela upozornenia cez Telegram.

## Technológie

**Hardware:** Raspberry Pi, CO2 senzor, magnetický senzor na okno

**Komunikácia:** MQTT broker

**Sensor modul:** Python

**Backend:** C# (.NET)

**Frontend:** TypeScript, React

**Notifikácie:** Telegram Bot API

**Infraštruktúra:** Docker, Docker Compose

## Architektúra
```
Raspberry Pi + Senzory (Python)
            |
            | MQTT
            v
      MQTT Broker
            |
            v
      Backend API (C#)
            |
            +---> Frontend Dashboard (TypeScript/React)
            |
            +---> Telegram Bot (notifikácie)
```

## Štruktúra projektu
```
iot-projekt/
├── sensor/           # Python skripty pre Raspberry Pi a MQTT publishing
├── backend/          # C# REST API + MQTT subscriber
├── frontend/         # React webová aplikácia
└── docker-compose.yml
```

## Funkcionality

- Real-time monitoring hodnôt CO2 cez MQTT
- Detekcia stavu okna (otvorené/zatvorené)
- Automatické odporúčania na vetranie
- Webový dashboard s aktuálnymi hodnotami
- Telegram notifikácie pri kritických hodnotách
- Historické dáta a grafy

## Spustenie
```bash
docker-compose up -d
```

## Autor

Patrik Staurovský

- GitHub: [github.com/patrik1531](https://github.com/patrik1531)