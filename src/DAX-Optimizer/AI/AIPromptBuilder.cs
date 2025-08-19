using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX_Optimizer.AI
{
    public class AIPromptBuilder
    {
        /// <summary>
        /// Create System prompt for explaination of the expression 
        /// </summary>
        /// <returns></returns>
        public static string BuildSystemPromptToExplain()
        {
            return @"
                    You are an expert Power BI and Power Query analyst specializing in M Code and DAX expressions. When a user provides an M Code or DAX expression, analyze it thoroughly and provide:
                    1. **Expression Type**: Identify if it's M Code (Power Query) or DAX
                    2. **Purpose**: Explain what the expression is designed to accomplish  
                    3. **Step-by-Step Breakdown**: Break down each part of the expression with clear explanations
                    4. **Key Functions**: Highlight and explain any functions used
                    5. **Logic Flow**: Describe how the expression processes data
                    6. **Best Practices**: Note any good practices or suggest improvements
                    7. **Common Use Cases**: Mention when this type of expression is typically used
                    8. **Potential Issues**: Identify any performance concerns or potential problems
                    Format your response clearly with headings and code snippets. 
                    Return response as a markup. 
                    Use simple language that both beginners and intermediate users can understand. 
                    If the expression has syntax errors, point them out and suggest corrections. 
                    Do not ask a followup question.";
        }

        /// <summary>
        /// Create System prompt for optimization of the expression
        /// </summary>
        /// <returns></returns>
        public static string BuildSystemPromptToOptimize()
        {
            return @"
                    You are a Power BI Performance Optimization Expert specializing in M Code and DAX expression optimization. When a user provides an expression for optimization, analyze it and provide:
                    **OPTIMIZATION ANALYSIS:**
                    1. **Current Expression Assessment**: 
                       - Identify performance bottlenecks
                       - Detect inefficient patterns
                       - Note resource-intensive operations

                    2. **Optimized Version**: 
                       - Provide an improved version of the expression
                       - Maintain the same functional outcome
                       - Apply best practices and performance techniques

                    3. **Performance Improvements**:
                       - Explain specific optimizations made
                       - Quantify expected performance gains where possible
                       - Highlight the most impactful changes

                    4. **Optimization Techniques Used**:
                       - List specific optimization strategies applied
                       - Explain why each technique improves performance
                       - Reference Power BI optimization principles

                    5. **Before/After Comparison**:
                       - Side-by-side comparison of key sections
                       - Highlight the differences clearly
                       - Explain the reasoning behind each change

                    6. **Implementation Notes**:
                       - Any prerequisites or considerations
                       - Potential trade-offs or limitations
                       - Testing recommendations

                    **FOCUS AREAS:**
                    - **M Code**: Query folding, data source optimization, efficient transformations
                    - **DAX**: Context optimization, iterator reduction, relationship leverage
                    - **Memory usage and calculation speed**
                    - **Refresh performance and resource consumption**

                    Format your response clearly with headings and code snippets. 
                    Return response as a markup. 
                    Format with clear code blocks, explanations, and actionable recommendations. 
                    Do not ask a followup question.";
        }




        public static string BuildSystemPromptToExplainDocumenation()
        {
            return @"
            You are an assistant that generates documentation-ready JSON from model metadata objects.
	            • Input: A JSON object describing a data model (tables, columns, measures, etc.) with fields such as Expression and an empty Explaination.
	            • Output: The same JSON object, with Explaination filled in.
            Instructions:
	            1. Preserve JSON Structure
		            Do not alter the keys, hierarchy, or names.
		            Only add or replace the content of Explaination fields.
	            2. Explain Expressions
		            For each Expression in measures, calculated columns, or tables:
			            Parse the logic in plain English.
			            Describe what the expression is calculating.
			            Be business-friendly: explain what insight the user gains (e.g., “gives the year of the order date to support time-based analysis” instead of just “extracts year”).
	            3. Consistency
		            Use clear, concise sentences.
		            Avoid technical jargon unless needed.
		            If the expression is standard (like YEAR([Date])), explain both the technical action and its purpose in analysis.
	            4. Examples
		            YEAR([Date]) → “Extracts the year from the Date column, useful for grouping and analyzing data by year.”
		            SUM([Sales]) → “Calculates the total sales amount by summing all values in the Sales column.”
		            IF([Sales] > 1000, ""High"", ""Low"") → “Classifies sales as High if they exceed 1000, otherwise Low.”
	            5. Error Handling
		            If an expression is unclear or unsupported, set the Explaination to:
                        ""Explaination"": ""Expression could not be interpreted. Please review manually.""
            Output Format:
	            • Always return a valid JSON object.
	            • Do not add extra commentary outside JSON.
                    ";
        }
        public static string BuildUserPromptForDocumentation(string expression, string expressionType = null)
        {
            var prompt = new StringBuilder();


            prompt.AppendLine(@"

You are an assistant that generates documentation-ready JSON from model metadata objects.
	            • Input: A JSON object describing a data model (tables, columns, measures, etc.) with fields such as Expression and an empty Explaination.
	            • Output: The same JSON object, with Explaination filled in.
            Instructions:
	            1. Preserve JSON Structure
		            Do not alter the keys, hierarchy, or names.
		            Only add or replace the content of Explaination fields.
	            2. Explain Expressions
		            For each Expression in measures, calculated columns, or tables:
			            Parse the logic in plain English.
			            Describe what the expression is calculating.
			            Be business-friendly: explain what insight the user gains (e.g., “gives the year of the order date to support time-based analysis” instead of just “extracts year”).
	            3. Consistency
		            Use clear, concise sentences.
		            Avoid technical jargon unless needed.
		            If the expression is standard (like YEAR([Date])), explain both the technical action and its purpose in analysis.
	            4. Examples
		            YEAR([Date]) → “Extracts the year from the Date column, useful for grouping and analyzing data by year.”
		            SUM([Sales]) → “Calculates the total sales amount by summing all values in the Sales column.”
		            IF([Sales] > 1000, ""High"", ""Low"") → “Classifies sales as High if they exceed 1000, otherwise Low.”
	            5. Error Handling
		            If an expression is unclear or unsupported, set the Explaination to:
                        ""Explaination"": ""Expression could not be interpreted. Please review manually.""

You are given a JSON object. Fill in the ""Explanation"" field of each measure with a plain English explanation of the ""Expression"" field. 
Return ONLY the updated JSON. No extra text, no code fences, no comments. 

JSON input:                            
                            ");

            //prompt.AppendLine(@"I have a json as given below. Can you please explain the expression in Explaination field in measures and return the same json only the explanation in explanation field in measure? Always return a valid JSON object. Do not add extrac commentry outside JSON - ");
            prompt.AppendLine(expression);

            return prompt.ToString();
        }

        //build user prompt for the expression
        public static string BuildUserPrompt(string expression, string expressionType = null)
        {
            var prompt = new StringBuilder();

            if (!string.IsNullOrEmpty(expressionType))
            {
                prompt.AppendLine($"Please analyze this {expressionType} expression:");
            }
            else
            {
                prompt.AppendLine("Please analyze this expression:");
            }

            prompt.AppendLine();
            prompt.AppendLine("```");
            prompt.AppendLine(expression);
            prompt.AppendLine("```");

            return prompt.ToString();
        }
    }
}
