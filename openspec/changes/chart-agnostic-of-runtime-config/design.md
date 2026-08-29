## Context

See proposal.md — Why.

The chart already has a well-developed convention for optional values, worked out over several
changes and documented at length in `deployment.yaml`: an explicit is-set test that treats only
`nil` and `""` as unset, so a `0` or a `false` reaches the pod and fails its startup validation
rather than silently falling back to a default. The `DocInt__*` limits, the admission block, the
metrics path, and the OTel exporter all follow it.

The two Foundry endpoints are the only values that opted out of that convention, via `required`.
This change does not invent a mechanism; it moves the endpoints onto the one already there.

`image.repository` is a different case. It has no sensible default *unless* the image's location is
known — which it now is, because `publish-to-ghcr` fixes it at `ghcr.io/eugo-as/eugo-docint`.

## Goals / Non-Goals

**Goals:**

- The chart renders with zero values supplied, so every `chart-lint` assertion can be made without
  knowing anything about Foundry.
- Endpoints follow the same is-set contract as every other optional value.
- The five dead `chart-lint` steps become live again without being edited, because what made them
  fail is gone.

**Non-Goals:**

- Weakening the service's boot-time endpoint check. It stays exactly as it is, and it is now the
  sole enforcement point.
- Changing the pod security context or the chart's volume mounts. Neither is touched — see
  *Verification*.
- Adding a values-level schema or precondition mechanism to replace the gate. Rejected below.

## Decisions

### `with`, not the explicit is-set test, for the endpoints

The `DocInt__*` limits need the explicit `if`/`kindIs` test because `0` and `false` are meaningful
values there and Helm's `with` treats both as empty. An endpoint is a URL: the only values are a
non-empty string or nothing, and `""` and unset mean the same thing. `with` is correct here and is
what the surrounding code already uses for `foundry.deploymentNameVision` and the OTel endpoint.

Using the heavier test anyway would suggest a distinction that does not exist, and the existing
comment block explaining why the limits *cannot* use `with` would become confusing if `with`
appeared nowhere nearby.

### `image.repository` gets a default rather than a softer gate

Removing `required` without a default renders `image: ":0.1.0"` — a manifest that fails at apply
time with a less useful message than the gate gave. The alternatives were to keep the gate (leaving
the chart un-renderable bare, which is the thing being fixed) or to default it.

Defaulting is only possible because the coordinate is now fixed. The value is
`ghcr.io/eugo-as/eugo-docint`, fully qualified: a bare `eugo-docint` would resolve against Docker
Hub and pull something unrelated, which is a worse failure than either previous option.

This creates a forward reference — the chart names a registry that `publish-to-ghcr` has not yet
published to. It is a default, overridable per release, and the only cost of the ordering is that
the default is briefly aspirational. Reversing the order instead would leave `chart-lint` red
through the whole of `publish-to-ghcr`, which is the larger of the two problems.

### The refusal test is inverted, not deleted

`ci.yml`'s "rendering without an endpoint fails and names the value" step asserted both halves of
the old contract. Its replacement asserts both halves of the new one: the render succeeds, **and**
neither endpoint variable appears. Deleting the step and asserting only that the render succeeds
would let a chart that silently emitted an empty-valued environment variable pass — which is the
failure this convention exists to prevent everywhere else in the chart.

### `$FOUNDRY` is removed from `ci.yml` rather than left in place

The job-level `env: FOUNDRY` exists only to satisfy the gate. Left behind, it would read as a value
those renders need, and the next person to add a step would copy it. The three steps that use it
lose it; the five `render()` helpers were already written as if the gate did not exist and are not
touched at all — which is the clearest possible evidence that they were written against the right
contract.

## Risks / Trade-offs

**A misconfigured release now fails as a crash-looping pod rather than at render.** → Accepted
deliberately; this is the point of the change. The service's boot failure names the missing endpoint
in exactly the same words the gate did, so the diagnostic is preserved even though its location
moves. The README documents both endpoints as required at run time.

**The five revived `chart-lint` steps have never actually run.** → They were written before the gate
and have been failing since it landed, so their assertions are unproven, not merely paused. Each one
must be confirmed green individually, and any that fails is a real finding about the chart rather
than a merge blocker to work around.

**The GHCR default is wrong if `publish-to-ghcr` is abandoned.** → It is one line in `values.yaml`
and a default at that. Accepted.

**Nothing now stops a release from installing with no endpoints at all.** → True, and it is the
inverse of the risk the gate was built for. The mitigation is that the pod fails immediately and
loudly rather than serving wrong results: docint has no degraded mode in which a missing endpoint
produces plausible output.

## Verification

The pod security context and the chart's volume mounts are **not** changed by this design. The
read-only root filesystem and the writable `/tmp` mount that XLSX extraction depends on are
untouched, so no real-pod verification is required on that account.

The `/tmp` mount assertion does change meaning, and for the better: it previously rendered from
"minimal values plus the two endpoints" and now renders from genuinely nothing, so it proves what it
always claimed to — that the mount depends on no configuration value.

## Deliberately deferred

- **A `values.schema.json` to validate values without gating the render.** The principled
  replacement for what is being removed: it can require an endpoint for a real install while leaving
  `helm template` free. Deferred because it is a second mechanism to learn and maintain, and because
  the service's boot check already reports the same fault. Worth revisiting if a release ever
  reaches a cluster without endpoints in practice.
- **A chart-level `NOTES.txt` warning when endpoints are unset.** Cheap and advisory, but it prints
  after a successful install and is easily missed; it would create the appearance of a safety net
  without the substance.
- **Auditing the other `chart-lint` steps for the same class of bug** — an assertion whose command
  cannot succeed and therefore proves nothing. Five were found here by accident. A deliberate sweep
  is worth doing, but it is not this change.
- **Giving `image.tag` a default too.** It already falls back to `.Chart.AppVersion`, which CI
  stamps at package time. No change needed, recorded so it is not re-derived.
