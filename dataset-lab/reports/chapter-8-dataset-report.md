# Dataset curation report

**Overall: PASS**

## Schema & label validation
- **train**: PASS (0 errors, 0 warnings)
- **eval**: PASS (0 errors, 0 warnings)

## Class balance
- **train**: PASS (50 records, imbalance ratio 1.00x)
  - Debit Without Final Status: 5
  - Duplicate Request: 5
  - Expired Card: 5
  - Insufficient Funds: 5
  - Invalid Beneficiary Details: 5
  - Pending Provider Confirmation: 5
  - Provider Authentication Failure: 5
  - Provider Timeout: 5
  - Provider Unavailable: 5
  - Webhook Delivery Failure: 5
- **eval**: PASS (20 records, imbalance ratio 1.00x)
  - Debit Without Final Status: 2
  - Duplicate Request: 2
  - Expired Card: 2
  - Insufficient Funds: 2
  - Invalid Beneficiary Details: 2
  - Pending Provider Confirmation: 2
  - Provider Authentication Failure: 2
  - Provider Timeout: 2
  - Provider Unavailable: 2
  - Webhook Delivery Failure: 2

## Duplicates
- **train**: PASS (0 exact groups, 0 near-duplicate pairs)
- **eval**: PASS (0 exact groups, 0 near-duplicate pairs)

## Train/eval leakage
- **train vs eval**: PASS (0 exact overlaps, 0 near-duplicate pairs)
