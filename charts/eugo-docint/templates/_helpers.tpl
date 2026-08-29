{{- /* A literal, deliberately NOT `.Chart.Name`. The chart is published as `eugo-docint-chart`
       -- `helm push` takes a registry namespace and reads the repository from the packaged
       Chart.yaml's `name:`, so the published name and the chart's name are the same string and
       neither can be chosen at push time. But this helper feeds app.kubernetes.io/name, which is
       part of the Deployment's spec.selector.matchLabels -- an immutable field. Letting the
       packaging name through would name every resource after its packaging format AND freeze the
       selector at that value, making every subsequent rename a delete-and-recreate.
       Do not "fix" this back to `.Chart.Name`. ci.yml's chart-lint asserts both halves: the
       rendered name stays `eugo-docint`, and `helm.sh/chart` below still follows the chart, which
       is right -- that label describes the artifact rather than the workload. */}}
{{- define "eugo-docint.name" -}}
{{- "eugo-docint" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- /* Built from the pinned name above rather than `.Chart.Name`, for the same reason: this is
       what names the Deployment, the Service, the ServiceAccount and the HPA, and those name the
       service, not the package it arrived in. */}}
{{- define "eugo-docint.fullname" -}}
{{- $name := include "eugo-docint.name" . -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- /* `.Chart.Name` on purpose, unlike the two helpers above: this label names the packaged
       artifact, and the artifact genuinely is `eugo-docint-chart`. */}}
{{- define "eugo-docint.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "eugo-docint.selectorLabels" -}}
app.kubernetes.io/name: {{ include "eugo-docint.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "eugo-docint.labels" -}}
helm.sh/chart: {{ include "eugo-docint.chart" . }}
{{ include "eugo-docint.selectorLabels" . }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{- define "eugo-docint.serviceAccountName" -}}
{{- if .Values.serviceAccount.create -}}
{{- default (include "eugo-docint.fullname" .) .Values.serviceAccount.name -}}
{{- else -}}
{{- default "default" .Values.serviceAccount.name -}}
{{- end -}}
{{- end -}}
