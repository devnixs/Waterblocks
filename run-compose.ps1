param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("full", "backend", "frontend")]
  [string]$Stack,
  [string]$Action = "up",
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$ExtraArgs
)

switch ($Stack) {
  "full" { $composeFile = "docker-compose.full.yml" }
  "backend" { $composeFile = "docker-compose.backend.yml" }
  "frontend" { $composeFile = "docker-compose.frontend.yml" }
}

if ($Action -eq "up") {
  docker compose -f $composeFile up -d @ExtraArgs
} else {
  docker compose -f $composeFile $Action @ExtraArgs
}
