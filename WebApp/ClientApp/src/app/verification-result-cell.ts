export class VerificationResultCell {
    value : string;
    flags : Array<string> = undefined;

    public constructor(newValue : string, newFlags : Array<string> = undefined)
    {
        this.value = newValue;
        this.flags = newFlags;
    }
}
