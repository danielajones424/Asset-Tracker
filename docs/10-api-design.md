# API Design — REST/JSON

**Status:** Draft for review. Base path `/api/v1`. OpenAPI 3.1 spec generated from code and published per build. All endpoints require an authenticated session unless noted; authorization per matrix in doc 09.

## 1. Conventions

- JSON bodies; `camelCase` fields; UUID ids; RFC 3339 timestamps (UTC).
- List endpoints: `?page`, `?pageSize` (≤100), `?sort=field:asc|desc`, plus resource-specific filters. Response envelope: `{ items, page, pageSize, totalCount }`.
- Errors: RFC 9457 problem+json — `{ type, title, status, detail, errors: [{field, message}], correlationId }`.
- Mutations require `X-Requested-With: XMLHttpRequest` (CSRF, doc 09).
- Idempotency: PUTs idempotent; uploads deduped by sha256.
- Versioning: path (`/v1`); additive changes don't bump.

## 2. Endpoints

### Auth & session
| Method | Path | Purpose |
|---|---|---|
| GET | /session | Current user, role, scope (bootstraps SPA) |
| POST | /session/logout | Destroy session |

### Profile
| GET/PUT | /profile | Own profile; identity fields ignored on PUT |

### Squadrons & units
| GET | /squadrons | List (scoped) |
| POST | /squadrons | Create (SysAdmin) |
| GET/PUT/DELETE | /squadrons/{id} | DELETE = deactivate; RESTRICT if active units |
| GET | /squadrons/{id}/units | Units in squadron |
| POST | /units · GET/PUT/DELETE /units/{id} | Same pattern |

### Users (SysAdmin)
| GET | /users?role&unitId&squadronId&q | |
| POST | /users | Provision (EDIPI, role, scope) |
| GET/PUT | /users/{id} | Role/scope/active changes audited |

### Assets
| GET | /assets?q&unitId&squadronId&deviceType&status&sort&page | Scoped search (FR-30) |
| POST | /assets | Create (custodian+) |
| GET/PUT | /assets/{id} | PUT = allowable fields per role |
| POST | /assets/{id}/status | `{ status, note }` — transition w/ history |
| GET | /assets/{id}/history | Field-level history (FR-31) |
| GET | /assets/{id}/documents | Source documents |
| POST | /assets/{id}/transfer-requests | Request move to another unit |
| POST | /transfer-requests/{id}/decision | Approve/deny (SqnAdmin+) |
| GET | /transfer-requests?status | Pending approvals |

### Documents & pipeline
| POST | /documents/upload-slots | `{ filename, sizeBytes, sha256, unitId }` → `{ documentId, presignedUrl, expiresAt }` |
| POST | /documents/{id}/uploaded | Client signals PUT complete → pipeline starts |
| GET | /documents?unitId&status&page | Document history (FR-41) |
| GET | /documents/{id} | Status, stages, failure reason, outcome summary |
| GET | /documents/{id}/candidates | Per-row outcomes (US-B3) |
| GET | /documents/{id}/content | Pre-signed GET for viewing (clean bucket only) |

### Review queue (SysAdmin)
| GET | /review-queue?sort=age&page | Pending candidates |
| GET | /review-queue/{candidateId} | Candidate + fields + confidence + duplicate matches |
| POST | /review-queue/{candidateId}/decision | `{ decision: accept\|correct\|merge\|reject, corrections?, mergeAssetId?, note? }` |

### Dashboards, reports, bulk
| GET | /dashboard | Scoped: counts by unit/type/status, queue depth, activity |
| GET | /reports/inventory?groupBy=unit\|squadron\|deviceType | |
| GET | /reports/processing?from&to | Throughput, auto-accept rate |
| POST | /bulk/import | CSV multipart → validation report `{ accepted, rejected: [{row, errors}] }` |
| GET | /bulk/export?same-filters-as-/assets | CSV stream; audited |

### Audit (SqnAdmin scoped / SysAdmin)
| GET | /audit?actor&entityType&entityId&action&from&to&page | Append-only view |

### Ops (unauthenticated, network-restricted)
| GET | /health/live · /health/ready | Liveness/readiness |

## 3. Status codes

200/201/204 success · 400 validation (problem+json with field errors) · 401 no/expired session · 403 out of scope or role · 404 not found *or out-of-scope read* (no existence leak) · 409 conflict (duplicate serial/tag, concurrent edit via `updatedAt` precondition) · 413 upload too large · 429 rate limited.

## 4. Shared contract

Request/response DTOs defined once in the shared F# project, compiled into the Fable client — client and server cannot drift (ADR-5). OpenAPI generated from the same types for external documentation.
