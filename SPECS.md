Waterblocks is a drop-in replacement for the Fireblocks API, designed specifically for testing crypto-trading platforms. It simulates blockchain operations using a fake, in-database blockchain, enabling comprehensive E2E and integration testing without interacting with real blockchain networks or risking real assets.
It will also be used in non-production environments to test transactions without having to use real assets.


The system provides two API surfaces:
1. **Fireblocks-compatible API** (22 endpoints) - Identical contract to production Fireblocks, allowing existing trading platform code to work unchanged
2. **Admin/Test API** - Full control over transaction states, failure simulation, and blockchain transaction creation for automated test scenarios


### What Makes This Special

- **Drop-in compatibility** - Mirrors Fireblocks API contract exactly, enabling seamless integration with existing trading platform code
- **Automation-first design** - All admin actions exposed via API for scripted test scenarios, not just manual UI operations
- **Complete transaction state machine** - Full lifecycle simulation with 11 states:
  `SUBMITTED → PENDING_AUTHORIZATION → PENDING_SIGNATURE → QUEUED → BROADCASTING → CONFIRMING → COMPLETED`
  Plus failure states: `FAILED`, `REJECTED`, `CANCELLED`, `TIMEOUT`
- **Failure mode simulation** - Simulate specific failure scenarios (insufficient funds, invalid address, network errors, timeouts) to test error handling paths
- **Timeout simulation** - Test slow confirmations, timeouts, and race conditions
- **Transaction-based seeding** - Create incoming blockchain transactions to naturally populate vault balances
- **Debugging companion UI** - Admin interface for real-time transaction state visualization, one-click state transitions, and debugging failed test scenarios, viewing vaults, assets etc
- **Test isolation via Docker** - Single shared application instance with Docker-based database isolation per test scenario


**Fireblocks-Compatible API:**
- All 22 endpoints matching Fireblocks API contract
- Complete transaction state machine (SUBMITTED, PENDING_SIGNATURE, PENDING_AUTHORIZATION, QUEUED, BROADCASTING, CONFIRMING, COMPLETED, FAILED, REJECTED, CANCELLED, TIMEOUT)
- Vault account management (create, list, hide/unhide)
- Asset and balance tracking
- Address generation and validation

**Admin/Test API:**
- Create incoming/outgoing blockchain transactions
- Transition transactions between states
- Simulate failure modes (insufficient funds, invalid address, network errors)
- Trigger timeouts

**Admin UI:**
- View all transactions and their current states
- View vault accounts and balances per asset
- View frozen balances
- One-click transaction state transitions (approve, sign, complete, fail, cancel)
- Transactions table are sorted by date desc and filterable by Asset, TransactionId, and TransactionHash.


#### Fireblocks-Compatible API (22 Endpoints)

**Vault Operations:**
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/vault/accounts_paged` | GET | List vault accounts (paginated) |
| `/vault/accounts` | GET/POST | List or create vault accounts |
| `/vault/accounts/{vaultAccountId}` | GET/PUT | Get or update vault account |
| `/vault/accounts/{vaultAccountId}/hide` | POST | Hide vault account |
| `/vault/accounts/{vaultAccountId}/unhide` | POST | Unhide vault account |
| `/vault/accounts/{vaultAccountId}/{assetId}` | GET/POST | Get or create wallet for asset |
| `/vault/accounts/{vaultAccountId}/{assetId}/balance` | POST | Refresh balance |
| `/vault/accounts/{vaultAccountId}/{assetId}/addresses` | GET/POST | Get or create addresses |
| `/vault/accounts/{vaultAccountId}/{assetId}/addresses_paginated` | GET | List addresses (paginated) |
| `/vault/accounts/{vaultAccountId}/{assetId}/unspent_inputs` | GET | Get UTXO inputs |
| `/vault/assets` | GET | List all vault assets |
| `/vault/assets/{assetId}` | GET | Get specific asset |

**Transaction Operations:**
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/transactions` | GET/POST | List or create transactions |
| `/transactions/estimate_fee` | POST | Estimate transaction fee |
| `/transactions/{txId}` | GET | Get transaction details |
| `/transactions/{txId}/drop` | POST | Drop transaction (ETH replacement) |
| `/transactions/{txId}/cancel` | POST | Cancel transaction |
| `/transactions/{txId}/freeze` | POST | Freeze transaction |
| `/transactions/{txId}/unfreeze` | POST | Unfreeze transaction |
| `/transactions/validate_address/{assetId}/{address}` | GET | Validate address |

**Utility:**
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/supported_assets` | GET | List supported assets |
| `/estimate_network_fee` | GET | Estimate network fees |

#### Admin/Test API

**Transaction Control:**
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/transactions` | GET/POST | List or create test transactions |
| `/admin/transactions/{id}` | GET | Get transaction details |
| `/admin/transactions/{id}/approve` | POST | Advance to PENDING_AUTHORIZATION |
| `/admin/transactions/{id}/sign` | POST | Advance to QUEUED/BROADCASTING |
| `/admin/transactions/{id}/broadcast` | POST | Advance to BROADCASTING |
| `/admin/transactions/{id}/confirm` | POST | Advance to CONFIRMING |
| `/admin/transactions/{id}/complete` | POST | Advance to COMPLETED |
| `/admin/transactions/{id}/fail` | POST | Set to FAILED with reason |
| `/admin/transactions/{id}/reject` | POST | Set to REJECTED |
| `/admin/transactions/{id}/cancel` | POST | Set to CANCELLED |
| `/admin/transactions/{id}/timeout` | POST | Trigger TIMEOUT |

**Vault Management:**
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/vaults` | GET/POST | List or create vaults |
| `/admin/vaults/{id}/frozen` | GET | View frozen assets |

### Authentication Model

| API Surface | Authentication Method |
|-------------|----------------------|
| Fireblocks-Compatible API | Mirror Fireblocks exactly (API key + JWT signing) |
| Admin/Test API | No authentication (internal test use only) |

### Data Schemas

- **Format:** JSON for all requests and responses
- **Serialization:** Exact match to Fireblocks API (amounts, addresses, transaction IDs, timestamps)
- **Schema Source:** Fireblocks swagger specification as authoritative source
- **Compatibility Goal:** Existing Fireblocks SDK/client code works unchanged

### Error Codes

Error responses must match Fireblocks error format exactly:
- Same HTTP status codes
- Same error response structure
- Same error codes and messages
- Admin API can trigger specific errors for testing error handling

### Rate Limits

- **Fireblocks-Compatible API:** No rate limits (tests run at maximum speed)
- **Admin API:** No rate limits
- **Rationale:** Testing tool prioritizes speed over realistic rate limiting

You have access to a Fireblocks.swagger.yml file that explains the contrat that has to be respected.


Document all of your work in AGENTS_XXX.md files. Feel free to create those files as needed, and reference them in the main AGENTS.md file