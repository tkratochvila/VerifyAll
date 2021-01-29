using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InterLayerLib
{
    class CLIPSVisitor : CLIPSBaseVisitor<string>
    {
        // Variables to be remembered for each rule and replaced with proper name
        // For example, (Pitch ?p) would generate ("p", "Pitch")
        private Dictionary<string, string> variables = new Dictionary<string, string>(); 

        /// <summary>
        /// file: construct+ EOF ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitFile(CLIPSParser.FileContext context)
        {
            string EARS = context.Start.InputStream.GetText(Antlr4.Runtime.Misc.Interval.Of(context.Start.StartIndex, context.Stop.StopIndex));
            EARS = Regex.Replace(EARS, "(^|\r\n)", "$1// "); // Keep the original CLIPS text just commented out
            // For each construct, convert it to EARS and separate them with two new lines:
            context.construct().ToList().ForEach(c => EARS += Environment.NewLine + Environment.NewLine + Visit(c));
            return Regex.Replace(EARS, "[\r\n]{5,}", Environment.NewLine + Environment.NewLine);
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
            var EARS = new StringBuilder();
            EARS.AppendLine($"ID \"{ context.id().GetText() }\":");

            if (context.textstring() != null)
                EARS.AppendLine($"// { context.textstring().GetText() }");
            if (context.declaration() != null)
                EARS.AppendLine($"// { context.declaration().GetText() }");
            EARS.Append("When ");
            EARS.AppendLine(String.Join(" and ", context.conditionalElement().ToList().
                Select(ce => Visit(ce)).Where(s=>s.Trim().Length > 0)));

            EARS.Append($"    then ");

            var responses = new SortedList<int, string>(context.ChildCount); // all visited responses in correct order
            context.aritmoper().ToList().ForEach(c => responses.Add(c.SourceInterval.a, Visit(c)));
            context.constant().ToList().ForEach(c => responses.Add(c.SourceInterval.a, Visit(c)));
            context.variable().ToList().ForEach(c => responses.Add(c.SourceInterval.a, Visit(c)));
            context.functionCall().ToList().ForEach(c => responses.Add(c.SourceInterval.a, Visit(c)));
            EARS.AppendLine(string.Join($" and ", responses.Values).Replace(" is ", " shall be "));

            Debug.Assert(! EARS.ToString().Contains("?"), $"There is an unused variable:\n{ EARS.ToString() }");
            EARS = EARS.Replace("'true'", "true").Replace("'false'", "false").Replace("''", " ");

            return EARS.ToString();
        }

        /// <summary>
        /// conditionalElement :
        ///LP (systemFunction|constant) (conditionalElement | variable | constant | textstring | aritmoper | oper | orOperation | systemFunction)*  RP 
        ///| aritmoper
		///| assignedpatternCE 
		///| notCE  
		///| orCE  
		///| logicalCE 
		///| testCE   
		///| existsCE  
		///| forallCE
        ///;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitConditionalElement(CLIPSParser.ConditionalElementContext context)
        {
            var EARS = new StringBuilder();

            if (context.orCE() != null)
                EARS.Append(Visit(context.orCE()));

            if (context.notCE() != null)
                EARS.Append(Visit(context.notCE()));

            if (context.existsCE() != null)
                EARS.Append(Visit(context.existsCE()));

#if CONCATENATION
            if (context.constant().Length + context.variable().Length <= 2)
                {
                    if (context.constant(0) != null)
                        EARS += Visit(context.constant(0)) + "___";// + " is ";

                    if (context.textstring(0) != null)
                        EARS += Visit(context.textstring(0));

                    if (context.constant(1) != null)
                        EARS += Visit(context.constant(1));

                    if (context.variable(0) != null)
                        EARS += Visit(context.variable(0));
                }

                if (context.constant().Length + context.variable().Length > 2)
                {
                    if (context.constant(0) != null)
                    {
                        EARS += Visit(context.constant(0)) + "___";// + " is in {";

                        for (int index = 1; index < context.constant().Length; index++)
                        {
                            EARS += Visit(context.constant(index)) + "___";
                        }

                        for (int index = 0; index < context.variable().Length; index++)
                        {
                            EARS += Visit(context.variable(index)) + "___";
                        }

                        if (EARS.EndsWith("___"))
                            EARS = EARS.Remove(EARS.LastIndexOf("___"));

                        // EARS += "}";
                    }
                }
                foreach (var ce in context.conditionalElement())
                    EARS += Visit(ce) + "___";
                if (EARS.EndsWith("___"))
                    EARS = EARS.Remove(EARS.LastIndexOf("___"));
#endif
#if ENUMERATION
                if (context.constant().Length + context.variable().Length <= 2)
                {
                    if (context.constant(0) != null)
                        EARS += Visit(context.constant(0)) + " is ";

                    if (context.textstring(0) != null)
                        EARS += Visit(context.textstring(0));

                    if (context.constant(1) != null)
                        EARS += "'" + Visit(context.constant(1)) + "'";

                    if (context.variable(0) != null)
                        EARS += "'" + Visit(context.variable(0)) + "'";

                }

                if (context.constant().Length + context.variable().Length > 2)
                {
                    if (context.constant(0) != null)
                    {
                        EARS += Visit(context.constant(0)) + " is '";

                        for (int index = 1; index < context.constant().Length; index++)
                        {
                            EARS += Visit(context.constant(index)) + " ";
                        }

                        for (int index = 0; index < context.variable().Length; index++)
                        {
                            EARS += Visit(context.variable(index)) + " ";
                        }

                        if (EARS.EndsWith(" "))
                            EARS = EARS.Remove(EARS.LastIndexOf(" "));
                        EARS += "'";
                        // EARS += "}";
                    }
                }

                //need to change this part to make it enumeration instead of concatenation
                foreach (var ce in context.conditionalElement())
                    EARS += "'" + ce.GetText() + "'";

#endif
#if HIERARCHICAL
            ///LP (systemFunction|constant) (conditionalElement | variable | constant | textstring | aritmoper | oper | orOperation | systemFunction)*  RP 
            string text = context.GetText();
            if (context.systemFunction(0) != null)

                if (context.textstring(0) != null)
                EARS.Append(Visit(context.textstring(0)));

            if (context.constant(0) != null)
            {
                string const0 = Visit(context.constant(0));
                if (context.conditionalElement().Count() > 0 
                    && context.conditionalElement(0).oper().Count() == 0 
                    && context.conditionalElement(0).aritmoper().Count() == 0)
                {
                    EARS.Append($"{ const0 }.{ string.Join(" and ", context.conditionalElement().Select(ce => Visit(ce))) }");
                }
                else
                {
                    EARS.Append($"{ const0 } is ");
                    var children = new SortedList<int, string>(); // all visited constants and variables in correct order
                    context.constant().Skip(1).ToList().ForEach(c => children.Add(c.SourceInterval.a, Visit(c)));
                    context.variable().ToList().ForEach(c => children.Add(c.SourceInterval.a, Visit(c)));
                    context.conditionalElement().ToList().ForEach(c => children.Add(c.SourceInterval.a, Visit(c)));
                    string allChildren = string.Join(".", children.Values);
                    if (! Regex.IsMatch(allChildren, @"(TRUE|FALSE|[0-9.]+)") && context.conditionalElement().Count() == 0)
                        allChildren = $"'{ allChildren }'";
                    EARS.Append(allChildren);
                }
            }
#endif

            context.aritmoper().ToList().ForEach(ao => EARS.Append(Visit(ao)));
            context.oper().ToList().ForEach(o => EARS.Append(Visit(o)));

            if (context.assignedpatternCE() != null)
                EARS.Append(Visit(context.assignedpatternCE()));

            if (context.testCE() != null)
                EARS.Append(Visit(context.testCE()));

            // Map the variables. For example, the fact (Pitch ?p) will generate EARS = "Pitch is ?p"
            Match m = Regex.Match(EARS.ToString(),
                @"([a-zA-Z][^ \t\r\n\u201D\u0028\u0029\u0026\u2758\u003F\u223C\u223B\u003C\u003E\u0024\u002A]*) is '\?([a-zA-Z][^ \t\r\n\u201D\u0028\u0029\u0026\u2758\u003F\u223C\u223B\u003C\u003E\u0024\u002A']*)'");
            if (m.Success)
            {
                variables.Add(m.Groups[2].ToString(), m.Groups[1].ToString());
                EARS.Clear();
            }
            else
            {
                foreach (KeyValuePair<string, string> kvp in variables)
                {
                    EARS = EARS.Replace($"?{ kvp.Key }", kvp.Value);
                }
            }
            return EARS.ToString();
        }


        /// <summary>
        /// notCE : LP 'not ' conditionalElement  RP ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>

        public override string VisitNotCE(CLIPSParser.NotCEContext context)
        {
            string EARS = "";
            EARS += Visit(context.conditionalElement());
            //EARS += " is false";
            //if (EARS.Contains(" and "))
            //   EARS = EARS.Replace(" and ", " is false and ");

            EARS = EARS.Replace(" is ", " is not ");

            // if (EARS.Contains(" or "))
            // EARS = EARS.Replace(" or ", " is false or ");
            return EARS;
        }

        /// <summary>
        /// existsCE : LP 'exists' WS* conditionalElement+  RP ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitExistsCE(CLIPSParser.ExistsCEContext context)
        {
            return string.Join(" and ", context.conditionalElement().Select(ce => Visit(ce)));
        }

        /// <summary>
        /// aritmoper : LP oper (aritmoper|constant|variable|conditionalElement|globalVariable) (aritmoper|constant|variable|conditionalElement|globalVariable)  RP  ;
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
            return String.Join(" or ", context.conditionalElement().Select(ce => Visit(ce)));
        }

        /// <summary>
        /// Makes any name safe for EARS notation
        /// </summary>
        /// <param name=""></param>
        static private string safeEARSName(string text)
        {
            switch (text)
            {
                // make first letter upper case
                case "action":
                case "absolute":
                case "according":
                case "after":
                case "all":
                case "any":
                case "are":
                case "as":
                case "at":
                case "average":
                case "based":
                case "be":
                case "becomes":
                case "been":
                case "before":
                case "between":
                case "by":
                case "column":
                case "condition":
                case "conditions":
                case "decrement":
                case "decremented":
                case "decrements":
                case "default":
                case "defined":
                case "difference":
                case "direction":
                case "end":
                case "equal":
                case "equals":
                case "every":
                case "exceeds":
                case "execution":
                case "expression":
                case "following":
                case "for":
                case "forever":
                case "from":
                case "greater":
                case "group":
                case "has":
                case "in":
                case "increment":
                case "incremented":
                case "increments":
                case "input":
                case "InterpolationTable":
                case "INTERPOLATIONTABLE":
                case "interpolationtable":
                case "invalid":
                case "is":
                case "its":
                case "km":
                case "kt":
                case "latch":
                case "least":
                case "less":
                case "limited":
                case "lower":
                case "member":
                case "mi":
                case "minute":
                case "most":
                case "ms":
                case "nm":
                case "none":
                case "number":
                case "of":
                case "on":
                case "order":
                case "otherwise":
                case "out":
                case "Output":
                case "outside":
                case "per":
                case "persistently":
                case "persists":
                case "precedence":
                case "previous":
                case "retain":
                case "requirement":
                case "requirements":
                case "row":
                case "satisfied":
                case "set":
                case "shall":
                case "square":
                case "start":
                case "state":
                case "sum":
                case "table":
                case "than":
                case "the":
                case "then":
                case "to":
                case "total":
                case "trace":
                case "transitions":
                case "until":
                case "upper":
                case "using":
                case "valid":
                case "value":
                case "within":
                case "second":
                case "seconds":
                case "sec":
                case "secs":
                case "ft":
                case "inch":
                case "deg":
                case "rad":
                case "millisecond":
                case "instance":
                    return $"{ text[0].ToString().ToUpper() }{ text.Substring(1).ToLower() }";
                // make remaining letters lower case
                case "AND":
                case "OR":
                case "NOT":
                case "ID":
                    return $"{ text[0] }{ text.Substring(1).ToLower() }";
                case "Define":
                case "define":
                case "If":
                case "if":
                case "Initial":
                case "initial":
                case "Initially":
                case "initially":
                case "When":
                case "when":
                case "Where":
                case "where":
                case "While":
                case "while":
                case "a":
                case "m":
                    return text.ToUpper();
                default:
                    return text;
            }
        }

        /// <summary>
        /// STRING | INT | FLOAT | booleansymbol
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitConstant(CLIPSParser.ConstantContext context)
        {
            string constant = safeEARSName(context.GetText());
            if (constant.Equals("TRUE") || constant.Equals("FALSE"))
                constant = constant.ToLower();
            return constant.StartsWith("-")?constant:constant.Replace("-", "__");
        }

        public override string VisitTextstring(CLIPSParser.TextstringContext context)
        {
            return context.GetText();
        }

        /// <summary>
        /// assignedpatternCE : variable '<-' conditionalElement ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitAssignedpatternCE(CLIPSParser.AssignedpatternCEContext context)
        {
            return (context.conditionalElement() != null) ? Visit(context.conditionalElement()) : "";
        }

        /// <summary>
        /// testCE : LP 'test' WS* (aritmoper|conditionalElement) RP;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitTestCE(CLIPSParser.TestCEContext context)
        {
            return (context.conditionalElement() != null)?Visit(context.conditionalElement()): Visit(context.aritmoper());
        }

        /// <summary>
        /// oper : NOTEQ | MUL | SUM | MINUS | DIV | LESS | LESSOREQ | EQUAL | GREATER | GREATEROREQ | LOGICALAND | LOGICALOR ;
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitOper(CLIPSParser.OperContext context)
        {
            if (context.NOTEQ() != null)
                return " is not ";
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
                return " and ";
            else //if (context.LOGICALOR() != null)
                return " or ";
        }

        /// <summary>
        /// orOperation : (constant|TEXT) ('|' (constant|TEXT))+ ; 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitOrOperation(CLIPSParser.OrOperationContext context)
        {
            string EARS = "";
            if (context.constant(0) != null)
                EARS += Visit(context.constant(0));

            if (context.TEXT(0) != null)
                EARS += context.TEXT(0).GetText();

            for (int index = 1; index < context.constant().Length; index++)
            {
                EARS += " or " + Visit(context.constant(index));
            }

            for (int index = 1; index < context.TEXT().Length; index++)
            {
                EARS += " or " + context.TEXT(index).GetText();
            }

            return EARS;
        }

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
            string functionName = context.functionName().GetText();
            var EARS = new StringBuilder();

            if (functionName.Equals("assert"))
            {
                EARS.Append(string.Join(" and ", context.conditionalElement().Select(ce => Visit(ce))));
            }
            else if (functionName.Equals("retract"))
            {
                foreach (var ce in context.variable())
                {
                    string varName = Visit(ce);

                    for (int i = 1; i < context.parent.ChildCount; i++)
                    {
                        if (context.parent.GetChild(i).GetText().Contains(varName))
                        { 
                            string whatToRetract = Visit(context.parent.GetChild(i)).Replace(" is ", " shall not be ");
                            EARS.Append($"{ whatToRetract } and ");
                            break;
                        }
                    }
                }
                return Regex.Replace(EARS.ToString(), " and $", "");
            }
            else if (functionName.Equals("send-output"))
            {
                EARS.Append("send___output shall be ");

                foreach (var ce in context.conditionalElement())
                {   // TODO this should be improved, and the "send-output" part should be eliminated otherwise than by starting with StartIndex+11
                    EARS.Append($"'{ ce.Start.InputStream.GetText(Antlr4.Runtime.Misc.Interval.Of(context.Start.StartIndex + 11, context.Stop.StopIndex)) }\' ");
                } 
            }

            return EARS.ToString();
        }
    }
}
