# 📘 README --- Génération du fichier `activations_mee.csv`

Ce script PowerShell lit automatiquement tous les fichiers Excel déposés
dans les sous-dossiers du répertoire `traitement/`, filtre les données
selon la date d'activation et le statut de migration, puis génère **un
fichier CSV par Excel**, situé dans le **même dossier** et portant **le
même nom** que le fichier source.

------------------------------------------------------------------------

## 📦 Prérequis

### 1. PowerShell 7+ recommandé

Le script utilise `-UseQuotes Never`, absent de Windows PowerShell 5.1.

### 2. Installer le module ImportExcel

``` powershell
Install-Module ImportExcel -Force
  ```

------------------------------------------------------------------------

## 🗂 Structure requise des dossiers

Votre arborescence doit ressembler à ceci :

    LireExcel.ps1
    traitement/
        mallette_client1/
            donnees_client1.xlsx
        mallette_client2/
            donnees_client2.xlsx

Chaque Excel génère :

    traitement/mallette_clientX/donnees_clientX.csv

------------------------------------------------------------------------

## Étape 1 : Préparer les Fichiers Excel

1. Créez un **sous-dossier** dans le dossier `traitement/` pour chaque mallette
2. Placez les fichiers Excel (`.xlsx`) des mallettes dans ces sous-dossiers

**Important** : Les fichiers Excel doivent contenir la feuille suivante :
- `Entreprises | Dossiers`

Vérifier que les noms des colonnes et entêtes correspondent :
- la colonne `source_id` (ligne 1) doit correspondre à l'entête `Identifiant source` (ligne2)
- la colonne `date_activation` (ligne 1) doit correspondre à l'entête `Date d'action souhaitée par la Plateforme RYDGE Conseil (format : JJ/MM/AAAA)` (ligne2)
- la colonne `statut_migration` (ligne 1) doit correspondre à l'entête `Statut de migration souhaitée sur la Plateforme RYDGE Conseil` (ligne2)

------------------------------------------------------------------------

## ▶️ Etape 2 : Exécution du script

Lancer la commande suivante en mettant en paramètre la date d'activation souhaitée :

``` powershell
pwsh .\LireExcel.ps1 -targetDate "dd/MM/yyyy"
```

------------------------------------------------------------------------

## 🔍 Fonctionnement du script

### 1. Recherche des fichiers Excel

Le script scanne récursivement :

    traitement/**.xlsx

Chaque fichier est traité indépendamment.

------------------------------------------------------------------------

### 2. Lecture et nettoyage des données

Pour chaque fichier :

-   Lecture de la feuille `"Entreprises | Dossiers"`
-   Suppression de la première ligne de données récupérée (header)
    (`Select-Object -Skip 1`)

------------------------------------------------------------------------

### 3. Filtre sur la date d'activation

Le script conserve uniquement les lignes où :

1.  La cellule `date_activation` contient une date valide\
    (les cellules contenant du texte non convertible sont **ignorées**)\
2.  La date correspond exactement à la date cible (ex: `04/12/2025`)\
    L'heure est ignorée.

Le script utilise `TryParse` en **culture française (fr-FR)** pour
supporter les formats `dd/MM/yyyy`.

------------------------------------------------------------------------

### 4. Filtre sur le statut de migration

Seuls les statuts suivants sont conservés :

-   `Activer l'offre Pennylane`
-   `Mettre en echec de migration`

Toutes les autres lignes sont exclues.

------------------------------------------------------------------------

### 5. Mapping du statut

  statut_migration               MigrationStatus
  ------------------------------ -----------------
  Activer l'offre Pennylane      success
  Mettre en echec de migration   failure

Toute autre valeur est ignorée.

------------------------------------------------------------------------

### 6. Construction des lignes CSV

Chaque entrée génère des colonnes :

-   `AccountNumber`
-   `ClientEmail`
-   `CollaboratorEmail`
-   `MissionLeaderEmail`
-   `AccountingExpertEmail`
-   `OfferName` (toujours `Pennylane`)
-   `MigrationStatus` (issu du mapping)

------------------------------------------------------------------------

### 8. Export des CSV finaux

Pour chaque Excel traité, le fichier CSV de sortie :

-   porte le **même nom**
-   est généré dans le **même dossier**
-   utilise le **séparateur `;`**
-   est encodé en **UTF‑8**
-   ne contient **aucune quote** (`-UseQuotes Never`)

Exemple :

    donnees_client1.xlsx → donnees_client1.csv

------------------------------------------------------------------------

## 📄 Exemple de CSV généré

    AccountNumber;ClientEmail;CollaboratorEmail;MissionLeaderEmail;AccountingExpertEmail;OfferName;MigrationStatus
    1000867814;;;;;Pennylane;success
    1000771089;;;;;Pennylane;failure

------------------------------------------------------------------------

### 9. Exécution de la requête d'activation/mise en échec

Il faut ensuite exécuter la requête suivante via POSTMAN depuis la VM de rebond de Prod
en passant en paramètre le contenu de chaque CSV généré dans le **body** :
`POST` `https://api.rydge.fr/registry/api/offer/update?token=XXX`

------------------------------------------------------------------------

### 10. Vérification

Depuis le front, dans l'onglet `Administration dossiers`, saisir un des IBS dans le champs de recherche pour accéder au dossier.
Dans l'onglet `Dossier > Offres`, vérifier que l'offre Pennylane a bien été activée/mise en échec de migration.

------------------------------------------------------------------------

## 📌 Notes

-   Les fichiers Excel sans les feuilles attendues sont ignorés.\
-   Les lignes contenant des dates non valides ou du texte dans `date_activation` sont écartées.\
-   Les e-mails dans les colonnes sont laissés vides volontairement.\
-   Des warnings apparaissent uniquement si un fichier Excel ne peut être lu.

------------------------------------------------------------------------
