export class DialogOption {
    text : string = "";
    callback : () => void = null;

    constructor(text : string, callback : () => void = null)
    {
        this.text = text;
        this.callback = callback;
    }
}
