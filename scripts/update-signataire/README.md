# Pennylane SSO Migration

Scripts de migration des clients vers l'authentification SSO `kpmg-customer` sur Pennylane.

---

## Prérequis

```bash
npm install
```

---

## Scripts


### `update-auth-method.js` — Mise à jour SSO

Pour chaque utilisateur trouvé dans `users-ids.csv` :
1. **GET** `/api/firm/v3/users/{id}` — vérifie la méthode d'authentification actuelle
2. Si déjà `kpmg-customer` → ignoré, aucune modification
3. Sinon → **PUT** `/api/firm/v3/users/{id}` avec `{ "authentication_method": "kpmg-customer" }`

```bash
node update-auth-method.js
```

**Entrée :** `users-ids.csv`  
**Sorties :** `update-results.csv` + `logs/run-<timestamp>.log`

#### Whitelist (feature flag)

Par défaut le script tourne en mode whitelist — seuls les IDs listés sont traités.

```js
// update-auth-method.js
const WHITELIST_ENABLED = true;
const WHITELIST = [
  'b9e29ee9-c0f1-4fc8-be33-1a0898ff76a9', // compte test
];
```

Pour un run complet sur toute la prod :
```js
const WHITELIST_ENABLED = false;
```

#### CSV de sortie `update-results.csv`

| Colonne | Valeurs possibles |
|---|---|
| `email` | — |
| `raison_sociale` | — |
| `id_akuiteo` | — |
| `id_pennylane` | — |
| `current_method` | méthode lue avant modification |
| `action` | `modifié` · `déjà-à-jour` · `erreur-get` · `erreur-put` · `erreur` |
| `http_status` | code HTTP retourné |
| `resultat` | `ok` · `ignoré` · `erreur` |

#### Rapport de fin d'exécution (log)

À chaque exécution un fichier `logs/run-<timestamp>.log` est créé, avec un rapport final :

```
╔══════════════════════════════════════════════════════════╗
  RAPPORT FINAL — 2026-04-30T14:22:05.000Z
  ENV       : app
  Whitelist : true
  Total traités   : 308
  ✔ Modifiés      : 241
  ~ Déjà à jour   : 52
  ✘ Erreurs       : 0
  ○ Ignorés (pas trouvés dans Pennylane) : 15
╚══════════════════════════════════════════════════════════╝

  ┌─ MODIFIÉS (241)
  │  b9e29ee9-...  toto@test.fr
  │  ...
  └────────────────────────────────────────────────────────

  ┌─ DÉJÀ À JOUR (non touchés) (52)
  │  ...
  └────────────────────────────────────────────────────────

  ┌─ IGNORÉS — pas trouvés dans Pennylane (15)
  │  ...
  └────────────────────────────────────────────────────────
```
