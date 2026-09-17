{{- define "waterblocks.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "waterblocks.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{- define "waterblocks.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "waterblocks.labels" -}}
helm.sh/chart: {{ include "waterblocks.chart" . }}
{{ include "waterblocks.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/part-of: waterblocks
{{- end }}

{{- define "waterblocks.selectorLabels" -}}
app.kubernetes.io/name: {{ include "waterblocks.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{- define "waterblocks.postgres.fullname" -}}
{{- printf "%s-postgres" (include "waterblocks.fullname" .) | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "waterblocks.api.fullname" -}}
{{- printf "%s-api" (include "waterblocks.fullname" .) | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "waterblocks.admin.fullname" -}}
{{- printf "%s-admin" (include "waterblocks.fullname" .) | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "waterblocks.postgres.secretName" -}}
{{- if .Values.postgres.auth.existingSecret -}}
{{- .Values.postgres.auth.existingSecret -}}
{{- else -}}
{{- include "waterblocks.postgres.fullname" . -}}
{{- end -}}
{{- end }}

{{- define "waterblocks.postgres.connectionString" -}}
{{- $host := include "waterblocks.postgres.fullname" . -}}
{{- printf "Host=%s;Port=5432;Database=%s;Username=%s;Password=%s;Include Error Detail=True" $host .Values.postgres.auth.database .Values.postgres.auth.username .Values.postgres.auth.password }}
{{- end }}

{{- define "waterblocks.externalDatabase.connectionString" -}}
{{- printf "Host=%s;Port=%v;Database=%s;Username=%s;Password=%s;Include Error Detail=True" .Values.externalDatabase.host (.Values.externalDatabase.port | default 5432) .Values.externalDatabase.database .Values.externalDatabase.username .Values.externalDatabase.password }}
{{- end }}

{{- define "waterblocks.database.validate" -}}
{{- if not .Values.postgres.enabled }}
{{- if .Values.externalDatabase.existingSecret }}
{{- else if .Values.externalDatabase.host }}
{{- else }}
{{- fail "When postgres.enabled is false, set externalDatabase.existingSecret or externalDatabase.host with credentials" }}
{{- end }}
{{- end }}
{{- end }}

{{- define "waterblocks.database.secretName" -}}
{{- if .Values.postgres.enabled -}}
{{- include "waterblocks.postgres.secretName" . -}}
{{- else if .Values.externalDatabase.existingSecret -}}
{{- .Values.externalDatabase.existingSecret -}}
{{- else -}}
{{- printf "%s-external-db" (include "waterblocks.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end }}

{{- define "waterblocks.database.secretKey" -}}
{{- if .Values.postgres.enabled -}}
DefaultConnection
{{- else -}}
{{- .Values.externalDatabase.secretKey | default "DefaultConnection" -}}
{{- end -}}
{{- end }}

{{- define "waterblocks.admin.apiBaseUrl" -}}
{{- if .Values.admin.config.apiBaseUrl }}
{{- .Values.admin.config.apiBaseUrl }}
{{- else if .Values.ingress.enabled }}
{{- printf "http://%s" .Values.ingress.hosts.api }}
{{- else }}
{{- fail "admin.config.apiBaseUrl must be set when ingress.enabled is false" }}
{{- end }}
{{- end }}
