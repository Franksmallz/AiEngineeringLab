# Prompt Engineering Experiment

## Objective

	Determine whether increase in prompt instructions leads to improved correctness, relevance, models instruction-following ability, latency, and cost.

##	Setup

	- Model: Claude Haiku
	- Evaluation cases: 11
	- Some cases across V1, V2, and V3
	- Same generation settings
	- Only prompt instructions changed

##	Results

	|         Metric              |     V1     |    V2    |    V3    |
	| Correctness                 |     4.18   |    4.64  |    4.27  |
	| Relevance                   |     4.73   |    4.27  |    4.73  |
	| Instruction-following       |     4.45   |    3.36  |    4.27  |
	| Overall quality             |     4.45   |    4.09  |    4.42  |
	| Avg latency                 |     5.07s  |    3.88s |    2.65s |
	| Avg cost/request            | $0.001892  |$0.001249 | $0.000843|
	| Total cost, 11 cases        | $0.020812  |$0.013735 | $0.009272|
	| Avg input tokens            |     -      |    190   |    250   |
	| Avg output tokens           |     -      |    212   |    119   |
	

## Key Observations

   - V1 was the baseline behaviour, the model had minimal guidance just to see what it can produce
   - V2 improved correctness but reduced Instruction-following
   - V3 restored relevance and instruction-following while producing much shorter outputs
   - V3 reduced average latency and cost substantially
   - Longer prompts did not necessarily increase total cost because V3 has reduced output token which in turn led to reduced cost
   - Language conversion difficulty and simplified technical cases continued to expose model limitations
   - Testing with larger models did not automatically solve the limitations.

## Decision

	- Use V3 with Haiku as the default prompt/model combination

## Limitations

	- Only 11 evaluation cases.
    - Scores were manually assigned.
    - V3 larger-model comparison covered only a difficult chemistry case
    - Results apply to this task set and should not be generalized to all workloads

##  Conclusion

    - Prompt engineering improved task fit and efficiency, but stricter prompting
    - V1 established the model's natural response style, which tended to be detailed and expansive. V2 introduced task-specific constraints, but the model's explanation was either verbose or ignored formatting requirements. V3 used stricter and more explicit rules, which produced shorter, more controlled responses and improved compliance on several cases.
    - However, it showed diminishing returns. Tnerefore critical output constraints should be backed by application-level validation rather than prompting alone	
	