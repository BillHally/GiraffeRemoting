Param([Parameter(Mandatory=$true)][ValidateSet("Create", "All")]$Action, $json)

#write-host $json

wsl curl `
    --insecure <# Allow self-signed certificates #> `
    <# -i show headers #> `
    --silent <# Don't show progress #> `
    --show-error <# but do show errors #> `
    -X POST `
    -H 'Content-Type: application/json' `
    -d "'`"$json`"'" `
    "https://localhost:5001/ModelOperations/$Action" `
    | ConvertFrom-Json
