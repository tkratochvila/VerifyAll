import { TextHighlighter } from "../text-highlighter"
import { HighlightItem } from "../highlight-item"
import grammar from './grammar.json';

interface GrammarGroup {
    group: string;
    terms: string[];
}

export class EarsHighlighter extends TextHighlighter {
    private _grammar : GrammarGroup[] = grammar;
    private _grammarDictionary : Map<string, GrammarGroup> = new Map();
    private _grammarRegex : RegExp = null;

    constructor()
    {
        super();
        this.highlighterClassName = "EARS";
        let regexString : string = "";

        for(let g of this._grammar)
        {
            for(let t of g.terms)
            {
                
                this._grammarDictionary.set(t, g);
                let term : string = t.replace(/[-[\]{}()*+?.,\\^$|#\s]/g, '\\$&');
                if(/^\w.*/.test(term))
                {
                    term = "(?<!\\w|\\.|_|-|\\d|\\$|\\?)" + term + "(?!\\w|\\.|_|-|\\d|\\$|\\?)";
                }

                regexString += term + "|";
            }
        }

        if(regexString.length > 0)
        {
            this._grammarRegex = new RegExp(regexString.slice(0, regexString.length - 1), "g");
        }
    }

    public generateHighlightedItems(text : string) : HighlightItem[]
    {
        if(this._grammarRegex)
        {
            var match : RegExpExecArray;
            var hItems : HighlightItem[] = [];
            var lastIndex : number = 0;
            while (match = this._grammarRegex.exec(text))
            {
                lastIndex = this.insertingNewHighlightItem(new HighlightItem(match.index, match.index+match[0].length - 1, this._grammarDictionary.get(match[0]).group), hItems, lastIndex) + 1;
            }
            
            return hItems;
        }
        
        return [];
    }
}
