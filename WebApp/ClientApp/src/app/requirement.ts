import { Subject } from 'rxjs';

export class Requirement {
    // public ID : string = undefined;
    public text : string = "";
    public focusEvent : Subject<void> = new Subject<void>();

    constructor(/*ID : string,*/ text : string)
    {
        // this.ID = ID;
        this.text = text;
    }
}
