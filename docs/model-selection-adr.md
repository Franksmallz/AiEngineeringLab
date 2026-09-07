# ADR: Model Selection

## Status

Accepted

## Context
- The AI engineering lab application needed a model that can provide accurate, relevant and instruction-following responses while keeping latency and cost reasonable. Therefore I had to evaluate claude Haiku and Claude Sonnet models using 11 datasets, 3 metrics with rubric scores.

## Decision
- Use claude Haiku as the default model for this application.
- Although Sonnet achieved slightly better quality but Haiku provided comparable results at substantially lower cost and lower latency. 
- Therefore Haiku is the default model and for harder tasks we can escalate to Sonnet

##Evidence
- Here's the break down for the result of the evaluation: 

- Overall quality:
  Haiku = 4.45/5
  Sonnet = 4.64/5

- Average latency:
  Haiku = 5.07S
  Sonnet = 7.21s

- Average cost: 
  Haiku = $0.00189/request
  Sonnet = $0.00581
  
- Based on the data above you can infer that even with slightly higher quality from sonnet it is expensive and slower to requests. However, Haiku offers quality, faster response time and it is cheaper. Therefore Haiku will be used as the default model and Sonnet as the escalation model.

##Consequences

- Positive
  lower operating cost
  lower latency
  good baseline quality
  
- Negative
  Haiku may perform worse on complex reasoning
  Some difficult instructions may require escalation
  
## When to revisit

- The decision to use Haiku is not cast in stone therefore there will be need to revisit this when:
  i.   Significant growth in evaluation dataset
  ii.  Changes in application use case
  iii. Changes in pricing of the Model
  iv.  Claude releases new cheaper models
  v.   Haiku fails important production cases
  vi   Change in quality requirements