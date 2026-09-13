# Chapter 6 RAG Evaluation Report

## Objective

This experiment compared two retrieval approaches over the same 18-question evaluation set:

- **Keyword retrieval** based on lexical overlap.
- **Embedding retrieval** based on semantic similarity using cosine similarity.

For each question, both systems used the retrieved chunks as context for the same generation step. Each result was manually scored on a 1–5 scale for:

- **Retrieval relevance** — whether the retrieved chunks contained the information needed to answer the question.
- **Correctness** — whether the generated answer matched the expected answer.
- **Groundedness** — whether the generated answer was supported by the retrieved context.

## Summary Results

| Metric | Keyword Retrieval | Embedding Retrieval |
|---|---:|---:|
| Retrieval relevance | 3.22/5 | 4.83/5 |
| Correctness | 3.33/5 | 4.61/5 |
| Groundedness | 5.00/5 | 4.94/5 |
| Average latency | 1184 ms | 1437 ms |
| Average generation cost | $0.001002 | $0.001022 |
| Total generation cost | $0.018035 | $0.018394 |

Embedding retrieval improved average retrieval relevance by **1.61 points** and correctness by **1.28 points**.

Embedding retrieval was about **21.4% slower** on average in this run. The difference in generation cost was small at about **2.0%**. This cost comparison does not include the cost of creating query embeddings unless that cost is already included in the application's recorded values.

## Win / Tie Breakdown

| Comparison | Keyword wins | Embedding wins | Ties |
|---|---:|---:|---:|
| Retrieval relevance | 0 | 13 | 5 |
| Correctness | 1 | 6 | 11 |

Embedding retrieval never lost a retrieval-relevance case in this evaluation set. Its largest advantage appeared when the question expressed the same concept using different wording or when the answer depended on retrieving the right section rather than simply matching common words.

## Important Observations

### 1. Embeddings handled semantic paraphrases better

Several questions used wording that differed from the exact wording in the documents. Keyword retrieval sometimes returned chunks with overlapping words but missed the actual answer. Embedding retrieval was better at locating the semantically relevant passage.

Strong examples were:

- **Case 2:** recognizing a previously processed request.
- **Case 10:** correcting an accounting error without rewriting history.
- **Case 15:** protecting webhook receivers from replayed messages.
- **Case 18:** connecting payment retries and webhook delivery through idempotency.

### 2. Keyword retrieval still worked well for direct questions

When the question reused the terminology found in the source documents, keyword retrieval often performed just as well as embeddings. Examples included:

- **Case 3:** repeated account increments.
- **Case 5:** retries with the same idempotency key.
- **Case 6:** retrying a payment after no response.
- **Case 16:** signing the raw webhook body.
- **Case 17:** deciding which payment record to trust.

This shows that keyword retrieval is still useful for direct lexical matches.

### 3. Better retrieval usually improved downstream correctness

The strongest correctness improvements appeared in cases where keyword retrieval failed to surface the required evidence. In those cases, the generation model behaved appropriately by refusing to invent an answer, but the final result was still incorrect relative to the expected answer.

Embedding retrieval improved the final answer because it improved the context supplied to the model.

### 4. Groundedness stayed high for both systems

Keyword groundedness averaged **5.00/5**, while embedding groundedness averaged **4.94/5**.

This is important because a response can be grounded while still being incorrect. If the retriever supplies incomplete context and the model says it cannot determine the answer, the response can still be faithfully grounded in the retrieved context.

### 5. Embedding retrieval is not guaranteed to find the right chunk

**Case 13** remained a failure for both approaches. The source document contained the needed information about acknowledging a webhook quickly and moving long-running work to a queue, but neither retriever returned that exact chunk.

This suggests that future improvements should focus on retrieval configuration rather than assuming embeddings alone solve every problem.

## Scoring Notes by Case

| ID | KR | ER | KC | EC | KG | EG |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 4 | 5 | 5 | 5 | 5 | 5 |
| 2 | 2 | 5 | 1 | 5 | 5 | 5 |
| 3 | 5 | 5 | 5 | 5 | 5 | 5 |
| 4 | 2 | 5 | 1 | 5 | 5 | 5 |
| 5 | 5 | 5 | 5 | 5 | 5 | 5 |
| 6 | 4 | 5 | 5 | 5 | 5 | 5 |
| 7 | 3 | 5 | 5 | 5 | 5 | 4 |
| 8 | 4 | 4 | 5 | 5 | 5 | 5 |
| 9 | 2 | 5 | 1 | 5 | 5 | 5 |
| 10 | 1 | 5 | 1 | 5 | 5 | 5 |
| 11 | 5 | 5 | 3 | 3 | 5 | 5 |
| 12 | 4 | 5 | 5 | 5 | 5 | 5 |
| 13 | 1 | 3 | 1 | 1 | 5 | 5 |
| 14 | 3 | 5 | 5 | 4 | 5 | 5 |
| 15 | 1 | 5 | 1 | 5 | 5 | 5 |
| 16 | 4 | 5 | 5 | 5 | 5 | 5 |
| 17 | 5 | 5 | 5 | 5 | 5 | 5 |
| 18 | 3 | 5 | 1 | 5 | 5 | 5 |

Legend:

- **KR** = Keyword Retrieval Relevance
- **ER** = Embedding Retrieval Relevance
- **KC** = Keyword Correctness
- **EC** = Embedding Correctness
- **KG** = Keyword Groundedness
- **EG** = Embedding Groundedness

## Conclusion

The experiment shows that embedding-based retrieval is a better default for this RAG system than simple keyword matching. The main benefit was not that the generation model became more capable; rather, the retriever supplied better evidence to the model.

Keyword retrieval remained competitive when the user question directly matched the source terminology, but it was less reliable when the question was paraphrased, conceptually phrased, or required a relationship across sections.

The next useful experiments are:

1. Tune `topK` and compare values such as 1, 3, and 5.
2. Add a minimum similarity threshold so weakly related chunks are excluded.
3. Review chunk boundaries, especially around sections that contain short but important facts.
4. Test hybrid retrieval by combining keyword and embedding scores.
5. Keep the same frozen evaluation set so future retrieval changes can be compared against this baseline.
