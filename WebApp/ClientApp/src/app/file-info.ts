export class FileInfo {
    fileName : string = "";
    info : string = null;

    constructor(fileName : string, info : string = null)
    {
        this.fileName = fileName;
        this.info = info;
    }
}
