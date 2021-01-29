
// CLIPS3.g4

grammar CLIPS;


@lexer::members {	
	public static string GrammarVersion {
		get {
			// THIS IS THE VERSION STRING
            // INCREMENT THIS for each (significant) check in
            return "0.1 2020-02-20";
		}
	}
	public static string GrammarName {
		get {
			return "CLIPS"; // Note: This should be the 'name' attribute in the 'Grammar' element used in Text2Test tool's command file
		}
	}
}

file: construct+ EOF ;

construct : 
			deffactsconstruct 
			| deftemplateconstruct 
			| defglobalconstruct 
			| defruleconstruct 
			| deffunctionconstruct 
			| defgenericconstruct 
			//| defmethodconstruct 
			//| defclassconstruct 
			//| definstanceconstruct 
			//| defmessagehandlerconstruct 
			| defmoduleconstruct
			| setstrategyconstruct
			;
 
deffactsconstruct : LP 'deffacts ' STRING textstring? rHSpattern* RP ;


deftemplateconstruct : LP 'deftemplate ' STRING textstring? slotdefinition* RP ;
 
 
defruleconstruct 
    : LP 'defrule ' id textstring? declaration? conditionalElement+ 
      '=>' ( LP  (constant | variable | functionCall | aritmoper)*  RP  )*  RP  ;



declaration : LP 'declare ' ruleproperty+  RP ;

ruleproperty : 
		LP 'salience ' (number|aritmoper|variable)  RP  
		| LP 'auto-focus' booleansymbol  RP ;

booleansymbol : 'TRUE' | 'FALSE';

LP : '(' ;
RP : ')' ; 

aritmoper : LP oper  (aritmoper|constant|variable|conditionalElement|globalVariable) (aritmoper|constant|variable|conditionalElement|globalVariable)  RP  ;
 
 
conditionalElement :
		LP  (systemFunction|constant) (conditionalElement | variable | constant | textstring | aritmoper | oper | orOperation | systemFunction)* RP 
		| aritmoper
		| assignedpatternCE 
		| notCE  
		| orCE  
		| logicalCE 
		| testCE   
		| existsCE  
		| forallCE  
		;

constant :  STRING | INT | FLOAT | booleansymbol;

assignedpatternCE : variable '<-' conditionalElement ;// LP  STRING+ (number|variable|conditionalElement|constant)*  RP;

notCE : LP 'not' WS* conditionalElement RP;

//andCE : LP LOGICALAND WS* conditionalElement+ RP;

orCE : ( LP LOGICALOR WS*) conditionalElement+ RP;

logicalCE : LP 'logical' WS* conditionalElement+ RP;

testCE : LP 'test' WS* (aritmoper|conditionalElement) RP;

existsCE : LP 'exists' WS* conditionalElement+ RP;

forallCE : LP 'forall' WS* conditionalElement conditionalElement+ RP;
 


  
slotdefinition : singleslotdefinition | multislotdefinition;

singleslotdefinition : LP 'slot ' slotName templateattribute* RP ;

multislotdefinition : LP 'multislot ' slotName templateattribute* RP ;

templateattribute : 
		defaultattribute 	
		| constraintattribute;

defaultattribute : 
		LP  ( 'default ' '?DERIVE' | '?NONE' | (constant | variable | functionCall )* )  RP  
		| LP 'defaultdynamic ' (constant | variable | functionCall )*  RP ;

rHSpattern : 
		orderedRHSpattern 
		| templateRHSpattern;

orderedRHSpattern : LP  STRING aRHSfield+ RP ;

templateRHSpattern : LP  STRING aRHSslot*  RP ;

aRHSslot : 
		singlefieldRHSslot 
		| multifieldRHSslot;

singlefieldRHSslot : LP  slotName aRHSfield  RP ;

multifieldRHSslot : LP  slotName aRHSfield*  RP ;

aRHSfield : 
		variable 
		| constant   
		| functionCall
		| textstring
		;
		
		


//functionCall :  functionName LP  (functionCall | constant | variable | textstring | LP  (constant | variable | textstring )+  RP  )*  RP ;
	
//functionCall :  functionName functionCall* conditionalElement+ ;	
	
functionCall :  functionName ( functionCall | constant | variable | conditionalElement )*;	

functionName : KEYWORD | STRING ;

number : FLOAT | INT | DIGIT ;

variable :
	singleFieldVariable
	| multiFieldVariable
	| globalVariable
	| wildcard
	;
	
	
singleFieldVariable : 	
	('?' STRING) 
	| ('?' STRING ':' aritmoper)	
	| ('?' STRING ('&:' aritmoper)+)
	| ('?' STRING ('&:' conditionalElement))
	| ('?' STRING '&' orOperation )
	| ( '?' STRING '&' (constant|TEXT) )
	;
	
systemFunction : STRING '$';
	
	
oper : NOTEQ | MUL | SUM | MINUS | DIV | LESS | LESSOREQ | EQUAL | GREATER | GREATEROREQ | LOGICALAND | LOGICALOR;

orOperation : (constant|TEXT) ('|' (constant|TEXT))+ ; 

wildcard : '$?' ;	
multiFieldVariable :('$?' STRING) ;
globalVariable : ('?*' STRING '*')	| ('*?' STRING) ;



defglobalconstruct : LP 'defglobal ' STRING? globalassignment+  RP ;

globalassignment : globalVariable '=' (constant | variable | functionCall);


deffunctionconstruct : LP 'deffunction ' STRING textstring? (regularparameter* wildcardparameter?) (constant | variable | functionCall)*  RP ;

regularparameter : singleFieldVariable;

wildcardparameter : multiFieldVariable;

defgenericconstruct : LP 'defgeneric ' STRING textstring?  RP ;
	
defmoduleconstruct : LP 'defmodule ' STRING textstring? portspecification*  RP ;

setstrategyconstruct: LP 'set-strategy' ('breadth'|'depth'|'lex'|'mea'|'complexity'|'simplicity'|'random') RP;

portspecification : 
		LP 'export ' portitem  RP  
		| LP 'import ' STRING portitem  RP ;

portitem :
		'?ALL' 
		| '?NONE' 
		| portconstruct '?ALL' 
		| portconstruct '?NONE' 
		| portconstruct STRING+;

portconstruct : 
		'deftemplate ' 
		| 'defclass ' 
		| 'defglobal ' 
		| 'deffunction ' 
		| 'defgeneric ';
	
	
	
	
//Constraint Attributes

constraintattribute : 
		typeattribute 
		| allowedconstantattribute 
		| rangeattribute 
		| cardinalityattribute
		;
		
typeattribute : LP 'type ' typespecification  RP  ;

typespecification : allowedtype+ | variable ;

allowedtype : STRING | number ;

allowedconstantattribute : 
		LP 'allowedsymbols ' symbollist  RP  
		| LP 'allowedstrings ' stringlist  RP  
		| LP 'allowedlexemes ' lexemelist  RP 
		| LP 'allowedintegers ' integerlist  RP  
		| LP 'allowedfloats ' floatlist  RP  
		| LP 'allowednumbers ' numberlist  RP  
		| LP 'allowedinstancenames ' instancelist  RP  
		| LP 'allowedclasses ' classnamelist  RP  
		| LP 'allowedvalues ' valuelist  RP ;

symbollist : STRING+ | variable;

stringlist : STRING+ | variable;

lexemelist : STRING+ | variable;

integerlist : INT+ | variable;

floatlist : FLOAT+ | variable;

numberlist : number+ | variable;

instancelist : STRING+ | variable;

classnamelist : STRING+ | variable;

valuelist : constant+ | variable;

rangeattribute : LP 'range ' rangespecification rangespecification  RP ;

rangespecification : number | variable;

cardinalityattribute : LP 'cardinality ' cardinalityspecification cardinalityspecification  RP ;

cardinalityspecification : INT | variable;


FLOAT : 
		INT EXPONENT 
		| INT ('.' EXPONENT)? 
		| '.' UINT EXPONENT? 
		| INT ('.' UINT EXPONENT)? 
		;
		

EXPONENT : ('e' | 'E') INT ;


LOGICALAND : '&' | 'and' ;
LOGICALOR : '|' | 'or' ;


INT : [+-\u2212]? DIGIT+ ;
UINT : DIGIT+ ;
DIGIT: [0-9] ;

NOTEQ : '!=' | '<>' | 'neq' ;
MUL : '*' ;
//**
SUM : '+';
MINUS : '-' ;
DIV : '/' ;
LESS : '<';
LESSOREQ : '<=';

EQUAL : '=';
GREATER : '>';
GREATEROREQ : '>=';

KEYWORD :
	'assert' | 'retract' | 'modify' | 'duplicate';

textstring : TEXT | EMPTYTEXT ;
EMPTYTEXT : '""' ;
TEXT : ('"' ~'"'* '"') ;


slotName : STRING ;

STRING : LETTER ~[ \t\r\n\u201D\u0028\u0029\u0026\u2758\u003F\u223C\u223B\u003C\u003E\u0024\u002A]* ;
	
id : STRING;
LETTER : [a-zA-Z] ; 


WS : [ \t\n\r]+ -> skip ;

PRINTOUT : LP  'printout' .*? 'crlf)' -> skip ;

SL_COMMENT
    :   ';' .*? ('\n' | EOF) -> skip
    ;

//LTRS : ~'"'+;
