import { Time } from "@angular/common";

export class Toust {
    public text : string = "";
    public timeOfCreation : Date;

    constructor(text : string)
    {
        this.text = text;
        this.timeOfCreation = new Date();
    }
}
