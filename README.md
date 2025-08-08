# R.A.T Server

## Description
Serveur de contrôle à distance (Remote Access Tool) permettant de gérer plusieurs clients connectés simultanément.

## Fonctionnalités
- Gestion de plusieurs clients en simultané
- Interface en ligne de commande interactive
- Commandes spéciales:
  - `!list` - Affiche la liste des clients connectés
  - `!switch <id>` - Change le client actif
  - `!all` - Mode diffusion vers tous les clients
- Support des commandes:
  - `INFO` - Obtient les informations système du client
  - `ECHO|<message>` - Envoie un message d'écho au client
  - `EXIT` - Déconnecte le client

## Installation
```bash
dotnet build
```

## Utilisation
```bash
dotnet run
```
Le serveur démarre sur le port 3001 par défaut.