export class TextChange {
    idx : number = null;
    prevText : string = null;
    newText : string = null;
    reason : string = null; // optional reason for text change

    constructor(idx : number, newText : string, prevText : string = "", reason : string = null) {
        this.idx = idx;
        this.newText = newText;
        this.prevText = prevText;
        this.reason = reason;
    }

}