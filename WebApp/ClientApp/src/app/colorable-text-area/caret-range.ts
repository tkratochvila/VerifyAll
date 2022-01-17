export class CaretRange {
    private _start : number;
    private _end : number;

    public constructor(start : number, end : number = null)
    {
        if(end == null)
        {
            end = start;
        }

        if(start < 0 || end < 0 || end < start)
        {
            this._start = -1;
            this._end = -1;
        }
        else
        {
            this._start = start;
            this._end = end;
        }
    }

    public get start() : number {
        return this._start;
    }

    public set start(value : number) {
        if(value < 0 || value > this._end)
        {
            this._start = -1;
            this._end = -1;
        }
        else
        {
            this._start = value;
        }
    }

    public get end() : number {
        return this._end;
    }

    public set end(value : number) {
        if(value < 0 || value < this._start)
        {
            this._start = -1;
            this._end = -1;
        }
        else
        {
            this._end = value;
        }
    }

    public isSelection() : boolean
    {
        return this._start != this._end;
    }

    public isValid() : boolean
    {
        return (this._start >=0 && this._end >=0);
    }
}
