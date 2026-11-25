# MongoDB Initialization

The script creates the Catalog collections and indexes used by the API. MongoDB's
`createCollection` and `createIndex` operations are safe to rerun with the same
names, so the command is idempotent.

Set `MONGODB_URI` and optionally `MONGODB_DATABASE` in the shell environment.
The URI is read by `mongosh` and is never printed by the script.

PowerShell:

```powershell
$env:MONGODB_URI = "mongodb://localhost:27017"
$env:MONGODB_DATABASE = "utttgo"
mongosh $env:MONGODB_URI scripts/db-init/init.js
```

Bash:

```bash
export MONGODB_URI='mongodb://localhost:27017'
export MONGODB_DATABASE='utttgo'
mongosh "$MONGODB_URI" scripts/db-init/init.js
```
