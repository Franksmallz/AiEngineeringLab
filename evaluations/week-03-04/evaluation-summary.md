## Objective
	- Comparing two claude proprietary models (Haiku and Sonnet) on correctness, relevance, instruction following, latency and cost.
	
## Dataset
	- 11
	- Simple factual explanations, instruction-following, translation, reasoning, complex domain questions
	
##  Evaluation Criteria
   a. Correctness
	  1 = incorrect
	  5 = full correct
   b. Relevance
      1 = mostly relevance
	  5 = highly focused
   c. Instruction-Following
      1 = ignores instructions
	  5 = follows instructions exactly
	  
## Evaluation method
    - Same evaluation dataset used for both models
	- Same prompts used
	- Each answer manually reviewed by me and GPT 5.6
	- Each answer scored from 1-5
	- Latency and estimated cost recorded automatically

## Result

   Metric                  |   Haiku       | Sonnet
   Correctness   		   |   4.18        | 4.45
   Relevance               |   4.73        | 4.82
   instruction-following   |   4.45        | 4.64
   Overall Quality         |   4.45        | 4.64
   Average Latency         |   5.07s       | 7.21s
   Average cost            |   $0.00189    | $0.00581
   
## Interesting Cases
   Case 8:
   Haiku produced contradictory possible answers
   Sonnet gave the intended answer directly

   Case 9: 
   Haiku violated the "the only answer in Italian" instruction by adding English explanation.
   
   Case 11:   
   Both models handled the three-bullet constraint well.
   
## Observations
   - Both models performed strongly on simple questions
   - Sonnet performed better on more difficult instruction-following Cases
   - Haiku was faster
   - Haiku was significantly cheaper
   - Many easy cases did not differentiate the models
   - Harder evaluation cases were more useful for model comparison
   
## Limitations
   - Dataset contained only 11 Cases
   - Several cases were relatively easy.
   - Scores were manually assigned and readjusted using ai as a judge.
   - Some expected behaviours were broad
   - Results only apply to this specific evaluation set
   
## Conclusion
   Sonnet achieved higher quality scores, while Haiku delivered similar performance with lower latency and significantly lower cost
   Sonnet achieved higher quality scores, while Haiku delivered similar performance with lower latency and significantly lower cost