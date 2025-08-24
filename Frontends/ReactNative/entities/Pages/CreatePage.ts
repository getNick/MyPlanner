import { PageType } from "./PageType";

export default class CreatePage {
    title: string;
    pageType: PageType;
    parentPageId?: string;
    constructor(title: string, pageType: PageType = "Folder", parentPageId: string | undefined = undefined) {
        this.title = title;
        this.pageType = pageType;
        this.parentPageId = parentPageId;
    }
}