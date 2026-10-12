**Objectif**
------------

Ce guide décrit la procédure pour analyser une mallette de migration (fichier Excel) dans le cadre des migrations vers Pennylane, en minimisant les risques sur la production et en facilitant l’identification des actions à mener.

* * *

**Étapes détaillées**
---------------------

### **1. Export des bases**

Exporter les bases `accprd01` et `regprd01` en fichiers BACPAC, en excluant le schéma `archive`, depuis la machine de rebond.
Pour regprd01 on a besoin que les schemas acc, contact, ref et reg


* * *

### **2. Import des bases et renommage**

Importer les fichiers BACPAC sur votre machine (serveur local), puis :
*   Renommer la base `accprd01` en `daaccprd01`
*   Renommer la base `regprd01` en `daregprd01`

* * *

### **3. Cloner le dépôt Pennylane**

Cloner le repository `Pennylane` et accéder au dossier `scripts`.

* * *

### **4. Télécharger, organiser et vérifier le fichier mallette**

*   Télécharger le fichier mallette reçu par Alex ou par le métier (ne traiter que les demandes validées par le PM pour assurer un suivi).
    
*   Sauvegarder ce fichier dans un sous-dossier sous `scripts/traitement`.
    **Exemple :**
    *   Fichier reçu :  
        `mallette_rydge_Vague_CaenSudNormandie_20250922 v envoyée N Tabary 13h54.xlsx`
    *   Chemin de sauvegarde :  
        `scripts/traitement/caen/mallette_rydge_Vague_CaenSudNormandie_20250922 v envoyée N Tabary 13h54.xlsx`
*   **Vérification des onglets :**
    *   Le fichier Excel doit obligatoirement contenir deux onglets nommés exactement :
        *   `Entreprises | Dossiers`
        *   `Dirigeants`
    *   Si ce n’est pas le cas (parfois, des mots peuvent être ajoutés ou modifiés), vous pouvez renommer les onglets pour correspondre à ces intitulés.
    *   **En cas de doute sur la bonne feuille, sollicitez le PM ou le TL pour confirmation.**
* S'assurer que la colonne lot , elle a  un ID "lot" dans la ligne 1
* * *

### **5. Lancer l’analyse – transformation du fichier Excel**

* Installer sur le poste utilsiateur ce module powershell : 
Install-Module -Name ImportExcel
*   Lancer le script `lireexcel.ps1`.
    
*   Ce script transforme le fichier Excel mallette en un fichier SQL, prêt à être importé dans la base locale sous forme de tables temporaires.
    **Après cette étape :**
    *   Un fichier `.sql` est automatiquement ajouté dans le sous-dossier `traitement`.
    *   Ce fichier contient les instructions SQL pour créer et alimenter les tables temporaires à partir des données du fichier mallette.

* * *

### **6. Exécuter le script d’analyse**

*   Exécuter le script `run_all_sql_and_analyse.ps1`.
*   Ce script :
    *   Exécute le fichier `.sql` généré (création et alimentation des tables temporaires dans la base locale).
    *   Exécute les fichiers d’analyse SQL déjà prêts.
    *   Génère automatiquement un fichier template d’analyse dans le même sous-dossier `traitement`, avec un nom commençant par `Analyse_`.
*   **À noter : le script traite automatiquement tous les fichiers présents dans les sous-dossiers de `traitement`.**  
    Il n’est donc pas nécessaire de lancer le script pour chaque fichier individuellement.

* * *

**Logique d’analyse**
---------------------

*   **Sécurité** : L’analyse est réalisée en local pour éviter tout risque sur la production.
*   **Méthodologie** : Les données sont importées dans des tables temporaires, permettant de faire des jointures complexes et d’extraire facilement les informations nécessaires (entreprises sans dirigeants, dirigeants non signataires, etc.).
*   **Suivi** : Toujours traiter uniquement les mallettes validées par le PM pour assurer la traçabilité et le suivi des migrations.

* * *

**Bonnes pratiques**
--------------------

*   Toujours vérifier la structure du fichier mallette avant de lancer l’analyse.
*   En cas de doute sur les onglets ou le contenu, demander confirmation au PM ou au TL.
*   Sauvegarder tous les fichiers d’entrée et de sortie dans des sous-dossiers dédiés pour chaque vague de migration.