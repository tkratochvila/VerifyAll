using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InterLayerLib
{
    class PETRIVisitor : CLIPSBaseVisitor<string>
    {
        // HashSet of all CLIPS facts encounered
        public HashSet<string> facts;
        // HashSet of the output facts encounered
        public HashSet<string> outputFacts;
        // Rule name
        public string ruleName { get; set; }
        public int ruleIndex { get; set; }

        public PETRIVisitor(int index)
        {
            ruleName = "Error: unknown rule name";
            ruleIndex = index;
            facts = new HashSet<string>();
            outputFacts = new HashSet<string>(); // facts used in fact-list commands (assert, etc.)
        }

        /// <summary>
        /// file: construct+ EOF ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitFile(CLIPSParser.FileContext context)
        {
            var GAL = new StringBuilder();
            GAL.Append(Regex.Replace(context.Start.InputStream.GetText(Antlr4.Runtime.Misc.Interval.Of(context.Start.StartIndex, context.Stop.StopIndex)),
                "(^|\r\n)", "$1  // ")); // Keep the original CLIPS text just commented out
            // For each construct, convert it to EARS and separate them with two new lines:
            context.construct().ToList().ForEach(c => GAL.Append($"{ Environment.NewLine }{ Visit(c) }{ Environment.NewLine }"));
            return Regex.Replace(GAL.ToString(), "[\r\n]{5,}", $"{ Environment.NewLine }{ Environment.NewLine }");
        }
        /// <summary>
        /// defruleconstruct 
        //     : LP 'defrule ' id textstring? declaration? conditionalElement+ 
        //       '=>' ( LP  (constant | variable | functionCall | aritmoper)+  RP  )+  RP  ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitDefruleconstruct(CLIPSParser.DefruleconstructContext context)
        {
            var GAL = new StringBuilder();
            Debug.Assert(ruleName == "Error: unknown rule name", "There sould be only one rule processed by this visitor at a time.");
            ruleName = context.id().GetText();
            GAL.Append($"  transition { ruleName }");

            if (context.textstring() != null)
                GAL.AppendLine($"  // { context.textstring().GetText() }");
            if (context.declaration() != null)
                GAL.AppendLine($"  // { context.declaration().GetText() }");
            GAL.Append($"    [ { String.Join(" && ", context.conditionalElement().Select(ce => $"{ Visit(ce) } == 1")) }");
            GAL.AppendLine(" ] {");
            GAL.AppendLine($"    rule_fired = { ruleIndex };");
            string response = "";
            response += String.Join(Environment.NewLine, context.conditionalElement().Select(ce => $"    { Visit(ce) } = -1;")) + Environment.NewLine;

            foreach (var ce in context.constant())
                response += $"{ Visit(ce) } = 1;{ Environment.NewLine }";
            foreach (var va in context.variable())
                response += $"{ Visit(va) } = 1;{ Environment.NewLine }";

            foreach (var ce in context.functionCall())
            {
                if (ce.GetText().StartsWith("retract"))
                { response += Visit(ce); }
                else
                { response += Visit(ce); }
            }
            foreach (var ce in context.aritmoper())
                response += Visit(ce) + " = 1;" + Environment.NewLine;

            GAL.Append($"{ response }  }}");

            GAL = GAL.Replace("?", "variable_");
            GAL = GAL.Replace("'true'", "true");
            GAL = GAL.Replace("'false'", "false");
            GAL = GAL.Replace("''", " ");

            return GAL.ToString();
        }

        /// <summary>
        /// conditionalElement :
        ///LP(systemFunction|constant) (conditionalElement | variable | constant | textstring | aritmoper | oper | orOperation | systemFunction)*  RP 
        ///| aritmoper
		///| assignedpatternCE 
		///| notCE  
		///|  orCE  
		///|  logicalCE 
		///|  testCE   
		///|  existsCE  
		///|  forallCE
        ///;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitConditionalElement(CLIPSParser.ConditionalElementContext context)
        {
            string GAL = "";

            if (context.orCE() != null)
                GAL += Visit(context.orCE());

            if (context.notCE() != null)
                GAL += Visit(context.notCE());

            if (context.existsCE() != null)
                GAL += Visit(context.existsCE());

#if PETRI_CONCATENATION

            if (context.parent.GetChild(1).GetText() == "defrule ")
               GAL += ruleName + "_";

            var simpleChildren = new SortedList<int, string>(); // all visited constants and variables in correct order
            context.constant().ToList().ForEach(c => simpleChildren.Add(c.SourceInterval.a, Visit(c)));
            context.variable().ToList().ForEach(c => simpleChildren.Add(c.SourceInterval.a, Visit(c)));
          
            GAL += string.Join("___", simpleChildren.Values);

            foreach (var ce in context.conditionalElement())
                GAL += "___" + Visit(ce);

            foreach (var ce in context.aritmoper())
                GAL += Visit(ce);

            foreach (var ce in context.oper())
                GAL += Visit(ce);

            if (context.assignedpatternCE() != null)
                GAL += Visit(context.assignedpatternCE());

            if (context.testCE() != null)
                GAL += Visit(context.testCE());

            // need to add the RuleName_ to the fact    /// 11 = aasignedpatternCE supposedly
            // TODO make it based on rulenames
            if (context.parent.RuleIndex != 9 && context.parent.RuleIndex != 11)
            {
                
                if (new[] { "assert", "retract", "modify", "duplicate" }
                    .Any(c => context.parent.GetChild(0).GetText().Equals(c)) )
                {
                    facts.Add($"{ ruleName }_{ GAL }");
                    outputFacts.Add(GAL);
                }
                else // Conditions	?color <- (traffic_light green)     
                {
                    facts.Add(GAL);
                }
            }
#endif
#if PETRI_ENUMERATION
                if (context.constant().Length + context.variable().Length <= 2)
                {
                    if (context.constant(0) != null)
                        GAL += Visit(context.constant(0)) + " = ";

                    if (context.textstring(0) != null)
                        GAL += Visit(context.textstring(0));

                    if (context.constant(1) != null)
                        GAL += "'" + Visit(context.constant(1)) + "'";

                    if (context.variable(0) != null)
                        GAL += "'" + Visit(context.variable(0)) + "'";
                }

                if (context.constant().Length + context.variable().Length > 2)
                {
                    if (context.constant(0) != null)
                    {
                        GAL += Visit(context.constant(0)) + " = '";

                        for (int index = 1; index < context.constant().Length; index++)
                        {
                            GAL += Visit(context.constant(index)) + " ";
                        }

                        for (int index = 0; index < context.variable().Length; index++)
                        {
                            GAL += Visit(context.variable(index)) + " ";
                        }

                        if (GAL.EndsWith(" "))
                            GAL = GAL.Remove(GAL.LastIndexOf(" "));
                        GAL += "'";
                        // GAL += "}";
                    }
                }

                //need to change this part to make it enumeration instead of concatenation
                foreach (var ce in context.conditionalElement())
                    GAL += "'" + ce.GetText() + "'";
#endif
#if PETRI_HIERARCHICAL
            string fact = "";
                    if (context.textstring(0) != null)
                    GAL += Visit(context.textstring(0));

                if (context.constant(0) != null) {
                    if (context.parent.GetChild(1).GetText() == "defrule ")
                        fact += context.parent.GetChild(2).GetText() + "_";
                    fact += Visit(context.constant(0));
                        for (int index = 1; index < context.constant().Length; index++)
                        fact += "_" + Visit(context.constant(index));
                        for (int index = 0; index < context.variable().Length; index++)
                        fact += "_" + Visit(context.variable(index));
                    GAL += fact;
                        }

                //need to change this part to make it enumeration instead of concatenation
                //foreach (var ce in context.conditionalElement())
                for (int index = 0; index < context.conditionalElement().Length; index++)
                {
                    fact = Visit(context.constant(0)) + "." + Visit(context.conditionalElement(index));
                    GAL = System.Text.RegularExpressions.Regex.Replace(GAL, " badly", "")
                        + ((index == 0) ? "" : " && ") + fact;
                }
                facts.Add(fact);
#endif
            return GAL;
        }


        /// <summary>
        /// notCE : LP 'not ' conditionalElement  RP ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>

        public override string VisitNotCE(CLIPSParser.NotCEContext context)
        {
            string GAL = "";
            GAL += Visit(context.conditionalElement());
            //GAL += " is false";
            //if (GAL.Contains(" and "))
            //   GAL = GAL.Replace(" and ", " is false and ");

            if (GAL.Contains(" == "))
                GAL = GAL.Replace(" == ", " != ");

            // if (GAL.Contains(" or "))
            // GAL = GAL.Replace(" or ", " is false or ");
            return GAL;
        }


        /// <summary>
        /// existsCE : LP 'exists' WS* conditionalElement+  RP ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitExistsCE(CLIPSParser.ExistsCEContext context)
        {
            return String.Join(" && ", context.conditionalElement().Select(ce => Visit(ce)));
        }

        /// <summary>
        /// aritmoper : LP oper  (aritmoper|constant|variable|conditionalElement|globalVariable) (aritmoper|constant|variable|conditionalElement|globalVariable)  RP  ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitAritmoper(CLIPSParser.AritmoperContext context)
        {
            var children = new SortedList<int, string>(context.ChildCount); // all visited children in correct order
            context.aritmoper().ToList().ForEach(c => children.Add(c.SourceInterval.a, Visit(c)));
            context.constant().ToList().ForEach(c => children.Add(c.SourceInterval.a, Visit(c)));
            context.variable().ToList().ForEach(c => children.Add(c.SourceInterval.a, Visit(c)));
            context.conditionalElement().ToList().ForEach(c => children.Add(c.SourceInterval.a, Visit(c)));
            context.globalVariable().ToList().ForEach(c => children.Add(c.SourceInterval.a, Visit(c)));
            return string.Join($" { context.oper().GetText() } ", children.Values);
        }

        /// <summary>
        /// orCE : ( LP LOGICALOR WS*) conditionalElement+  RP ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitOrCE(CLIPSParser.OrCEContext context)
        {
            return String.Join(" || ", context.conditionalElement().Select(ce => Visit(ce)));
        }

        public override string VisitConstant(CLIPSParser.ConstantContext context)
        {
            return context.GetText().Replace("-", "__");
        }

        public override string VisitTextstring(CLIPSParser.TextstringContext context)
        {
            return context.GetText();
        }

        /// <summary>
        /// assignedpatternCE : variable '<-' conditionalElement;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitAssignedpatternCE(CLIPSParser.AssignedpatternCEContext context)
        {
            return (context.conditionalElement() != null) ? Visit(context.conditionalElement()) : "";
        }

        /// <summary>
        /// testCE : LP 'test' WS* (aritmoper|conditionalElement)    RP ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitTestCE(CLIPSParser.TestCEContext context)
        {
            return (context.conditionalElement() != null) ? Visit(context.conditionalElement()) : Visit(context.aritmoper());
        }

        /// <summary>
        /// oper : NOTEQ | MUL | SUM | MINUS | DIV | LESS | LESSOREQ | EQUAL | GREATER | GREATEROREQ | LOGICALAND | LOGICALOR ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitOper(CLIPSParser.OperContext context)
        {
            if (context.NOTEQ() != null)
                return " != ";
            else if (context.MUL() != null)
                return " * ";
            else if (context.SUM() != null)
                return " + ";
            else if (context.MINUS() != null)
                return " - ";
            else if (context.DIV() != null)
                return " / ";
            else if (context.LESS() != null)
                return " < ";
            else if (context.LESSOREQ() != null)
                return " <= ";
            else if (context.GREATER() != null)
                return " > ";
            else if (context.GREATEROREQ() != null)
                return " >= ";
            else if (context.LOGICALAND() != null)
                return " && ";
            else //if (context.LOGICALOR() != null)
                return " || ";
        }

        /// <summary>
        /// orOperation : (constant|TEXT) ('|' (constant|TEXT))+ ; 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitOrOperation(CLIPSParser.OrOperationContext context)
        {
            string GAL = "";
            if (context.constant(0) != null)
                GAL += Visit(context.constant(0));

            if (context.TEXT(0) != null)
                GAL += context.TEXT(0).GetText();

            for (int index = 1; index < context.constant().Length; index++)
            {
                GAL += " || " + Visit(context.constant(index));
            }

            for (int index = 1; index < context.TEXT().Length; index++)
            {
                GAL += " || " + context.TEXT(index).GetText();
            }

            return GAL;
        }

        /*
        public override string VisitVariable(CLIPSParser.VariableContext context)
        {
            string GAL = "";
            GAL += context.GetText();

            if (GAL.Contains("-"))
                GAL = GAL.Replace("-", "__");

            return GAL;
        }
        */

        public override string VisitVariable(CLIPSParser.VariableContext context)
        {
            return context.GetText().Replace("-", "__");
        }


        /// <summary>
        /// functionCall :  functionName functionCall* conditionalElement+ ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitFunctionCall(CLIPSParser.FunctionCallContext context)
        {
            var GAL = new StringBuilder();
            string functionName = context.functionName().GetText();
            GAL.Append($"\n //{ functionName.ToUpper() }: ");
            if (functionName.Equals("assert"))
            {
                context.conditionalElement().ToList().ForEach(ce => GAL.Append(Visit(ce)));
                GAL.Append( Environment.NewLine );
                }
            else if (new[] { "retract", "modify", "duplicate" }
                .Any(c => functionName.Equals(c)) )
            {
                foreach (var ce in context.variable())
                {
                    string varName = Visit(ce);

                    for (int i = 1; i < context.parent.ChildCount; i++)
                    {
                        if (context.parent.GetChild(i).GetText().Contains(varName))
                        {
                            GAL.Append(Visit(context.parent.GetChild(i).GetChild(0).GetChild(2)));
                            break;
                        }
                    }
                }
            }
            else if (functionName.Equals("send-output"))
            {
                GAL.Append("send___output shall be "); // TODO Fix

                foreach (var ce in context.conditionalElement())
                {   // this should be improved, and the "send-output" part should be eliminated otherwise than by starting with StartIndex+11
                    GAL.Append($"'{ ce.Start.InputStream.GetText(Antlr4.Runtime.Misc.Interval.Of(context.Start.StartIndex + 11, context.Stop.StopIndex)) }' ");
            }
            }

            return GAL.ToString();
        }

    }
}
