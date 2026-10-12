# Export-PennylaneCompanies

Script PowerShell pour exporter les companies Pennylane dont l'ID est un GUID vers un fichier CSV.

## Prerequis

- PowerShell 5.1 ou superieur
- Une cle API Pennylane valide

## Utilisation

```powershell
.\Export-PennylaneCompanies.ps1 -ApiKey "votre_api_key"
```

## Parametres

| Parametre     | Obligatoire | Defaut                                       | Description                            |
| ------------- | ----------- | -------------------------------------------- | -------------------------------------- |
| `-ApiKey`     | Oui         | -                                            | Cle API pour l'authentification Bearer, found in the app settings of pennylane web app in azure under the variable with the name `PennylaneServiceSettings__ApiKey`  |
| `-ApiUrl`     | Non         | `https://sandbox.pennylane.com/api/firm/v3/` | URL de base de l'API Pennylane, found in the app settings of pennylane web app in azure under the variable with the name `PennylaneServiceSettings__ApiUrl`      |
| `-OutputPath` | Non         | `PennylaneCompanies.csv`                     | Chemin du fichier CSV de sortie        |

## Exemples

### Export basique
```powershell
.\Export-PennylaneCompanies.ps1 -ApiKey "votre_api_key"
```

### Export vers un chemin specifique
```powershell
.\Export-PennylaneCompanies.ps1 -ApiKey "votre_api_key" -OutputPath "C:\exports\companies.csv"
```

### Utilisation avec l'API de production
```powershell
.\Export-PennylaneCompanies.ps1 -ApiKey "votre_api_key" -ApiUrl "https://app.pennylane.com/api/firm/v3/"
```

## Format de sortie

Le fichier CSV genere contient les colonnes suivantes (separateur `;`) :

| Colonne     | Description                             |
| ----------- | --------------------------------------- |
| `Id`        | Identifiant unique de la company (GUID) |
| `RegNo`     | Numero d'enregistrement (SIREN/SIRET)   |
| `Name`      | Nom de la company                       |
| `TradeName` | Nom commercial                          |

## Fonctionnement

1. Le script se connecte a l'API Pennylane avec la cle API fournie
2. Il recupere toutes les companies par pagination (500 par page)
3. La pagination continue jusqu'a recevoir une liste vide
4. Seules les companies avec un ID au format GUID sont conservees
5. Les donnees sont exportees dans un fichier CSV avec le separateur `;`
