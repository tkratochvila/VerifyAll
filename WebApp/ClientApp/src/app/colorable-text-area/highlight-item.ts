export class HighlightItem {
    start : number;
    end : number;
    styleClass : string;
    comment : string;

    constructor(start : number, end: number, styleClass : string, comment : string = "")
    {
        this.start = start;
        this.end = end;
        this.styleClass = styleClass;
        this.comment = comment;
    }
}
