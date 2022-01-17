import { Subject } from 'rxjs';
import { ColorableTextAreaComponent } from './colorable-text-area/colorable-text-area.component';
import { HighlightItem } from './colorable-text-area/highlight-item';
import { TextChange } from './colorable-text-area/text-change'

export class Requirement {
    public ID : string = null;
    public text : string = "";
    public highlights : HighlightItem[] = [];
    public focusEvent : Subject<void> = new Subject<void>();
    public changeTextEvent : Subject<TextChange> = new Subject<TextChange>();
    public afterChangeAnalysisTimeout : NodeJS.Timeout = null;

    constructor(ID : string, text : string)
    {
        this.ID = ID;
        this.text = text;
    }

    public textHash() : number
    {
        var hash = 0, i = 0, len = this.text.length;
        while ( i < len ) {
            hash  = ((hash << 5) - hash + this.text.charCodeAt(i++)) << 0;
        }

        return hash;
    }
}
