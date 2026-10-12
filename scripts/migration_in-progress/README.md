# Migration Pennylane - Scripts d'Extraction

Ce dossier contient les scripts PowerShell permettant de traiter les fichiers Excel des mallettes Pennylane et de générer des fichiers CSV prêts à être envoyés vers l'API Registry.

## Prérequis

- PowerShell 5.1 ou supérieur
- SQL Server LocalDB installé
- Module PowerShell `ImportExcel` installé :
  ```powershell
  Install-Module -Name ImportExcel -Scope CurrentUser
  ```

## Architecture du Projet

```
migration_in-progress/
├── LireExcel.ps1       # Script 1 : Lit les fichiers Excel et génère les fichiers SQL
├── export.ps1          # Script 2 : Exécute les SQL et génère les fichiers CSV finaux
├── export.sql          # Requête d'extraction des données au format attendu
├── traitement/         # Dossier contenant les fichiers Excel à traiter
│   └── [malette1]/     # Sous-dossiers par mallette
│       └── fichier.xlsx
└── README.md
```

## Étape 1 : Créer la Base de Données Locale

Avant de commencer, vous devez créer une base de données locale vide :

```powershell
# Connexion à LocalDB
sqllocaldb start MSSQLLocalDB

# Créer la base de données
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE daregprd01"
```

> **Note** : Le nom de la base `daregprd01` est défini dans le script `export.ps1` (ligne 9). Vous pouvez le modifier si nécessaire.

## Étape 2 : Préparer les Fichiers Excel

1. Créez un **sous-dossier** dans le dossier `traitement/` pour chaque mallette
2. Placez les fichiers Excel (`.xlsx`) des mallettes dans ces sous-dossiers

**Structure attendue :**
```
traitement/
├── mallette_client1/
│   └── donnees_client1.xlsx
├── mallette_client2/
│   └── donnees_client2.xlsx
└── ...
```

**Important** : Les fichiers Excel doivent contenir les feuilles suivantes :
- `Entreprises | Dossiers`
- `Utilisateurs Dirigeants`

## Étape 3 : Générer les Fichiers SQL

Exécutez le script `LireExcel.ps1` pour lire les fichiers Excel et générer les scripts SQL correspondants :

```powershell
.\LireExcel.ps1
```

**Ce script va :**
- Parcourir tous les fichiers `.xlsx` dans les sous-dossiers de `traitement/`
- Extraire les données des feuilles Excel
- Générer des fichiers `.sql` à côté de chaque fichier Excel
- Ces fichiers SQL créent des tables temporaires (`tmp.*`) et insèrent les données

**Résultat :**
```
traitement/
├── mallette_client1/
│   ├── donnees_client1.xlsx
│   └── donnees_client1.xlsx.sql    ← Fichier généré
```

## Étape 4 : Extraire les Données en CSV

Exécutez le script `export.ps1` pour exécuter les scripts SQL et générer les fichiers CSV finaux :

```powershell
.\export.ps1
```

**Ce script va :**
1. Supprimer les tables temporaires existantes
2. Exécuter chaque fichier `.sql` trouvé dans `traitement/`
3. Créer les tables temporaires et insérer les données
4. Exécuter la requête `export.sql` pour extraire les données au format attendu
5. Générer un fichier `.csv` à côté de chaque fichier `.sql`

**Résultat :**
```
traitement/
├── mallette_client1/
│   ├── donnees_client1.xlsx
│   ├── donnees_client1.xlsx.sql
│   └── donnees_client1.xlsx.csv    ← Fichier CSV généré
```

## Étape 5 : Vérifier les Fichiers CSV

**Important** : Avant d'envoyer les données à l'API, vérifiez visuellement chaque fichier CSV :

1. Ouvrez les fichiers `.csv` avec un éditeur de texte ou Excel
2. Vérifiez que :
   - Les données sont présentes et complètes
   - Les emails sont valides
   - Les AccountNumber sont corrects
   - Il n'y a pas de caractères spéciaux mal encodés
   - Le format correspond à celui attendu (voir ci-dessous)

**Format attendu du CSV :**
```csv
AccountNumber;ClientEmail;CollaboratorEmail;MissionLeaderEmail;AccountingExpertEmail;OfferName;MigrationStatus
12345;client@example.com;collab@example.com;leader@example.com;expert@example.com;Pennylane;in_progress
```

**Colonnes :**
- `AccountNumber` : Numéro du dossier client
- `ClientEmail` : Email du dirigeant
- `CollaboratorEmail` : Email du comptable assigné
- `MissionLeaderEmail` : Email du superviseur comptable
- `AccountingExpertEmail` : Email du manager comptable
- `OfferName` : Toujours `Pennylane`
- `MigrationStatus` : Toujours `in_progress`

## Étape 6 : Envoyer les Données à l'API

Une fois les CSV vérifiés, envoyez les données à l'API Registry :

1. Ouvrez **Postman**
2. Créez une nouvelle requête `POST`
3. URL : `[votre-endpoint]/registry/offers`
4. Headers :
   ```
   Content-Type: text/csv
   ```
5. Body : Sélectionnez `raw` et copiez-collez le contenu du fichier CSV
6. Envoyez la requête

**Répétez cette opération pour chaque fichier CSV généré.**

## Structure des Données

### Tables Temporaires Créées

Les scripts créent deux tables temporaires dans le schéma `tmp` :

- `tmp.Entreprises___Dossiers` : Informations sur les dossiers clients
- `tmp.dirigeants` : Informations sur les dirigeants

### Requête d'Extraction (export.sql)

La requête `export.sql` effectue une jointure entre ces deux tables pour produire le format CSV final attendu par l'API.

## Dépannage

### Erreur : "Le fichier export.sql n'existe pas"
Vérifiez que le fichier `export.sql` est bien présent dans le dossier `migration_in-progress/`.

### Erreur : "Cannot find path"
Assurez-vous que :
- Le dossier `traitement/` existe
- Les fichiers Excel sont dans des **sous-dossiers** de `traitement/` (pas directement dans `traitement/`)

### Erreur : "Database does not exist"
Créez la base de données `daregprd01` (voir Étape 1) ou modifiez le nom de la base dans `export.ps1` ligne 9.

### Module ImportExcel non trouvé
Installez le module :
```powershell
Install-Module -Name ImportExcel -Scope CurrentUser -Force
```

### Les CSV sont vides
Vérifiez que :
- Les fichiers Excel contiennent bien des données
- Les noms des feuilles correspondent exactement à `Entreprises | Dossiers` et `Utilisateurs Dirigeants`
- Les fichiers `.sql` ont bien été générés

## Notes Importantes

- Les scripts utilisent la base de données `(localdb)\MSSQLLocalDB`
- Les tables temporaires sont créées dans le schéma `tmp`
- Les tables temporaires sont supprimées et recréées à chaque exécution
- Les fichiers CSV sont encodés en UTF-8
- Le séparateur des CSV est le point-virgule (`;`)

## Support

Pour toute question ou problème, contactez l'équipe technique.
