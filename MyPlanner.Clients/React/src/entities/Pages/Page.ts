import PageContent from "./PageContent";

export default class Page {
    id: string;
    title: string;
    pages: Page[];
    content?: PageContent;
    constructor(id: string, title: string, pages: Page[] = [], content: PageContent | undefined = undefined) {
        this.id = id;
        this.title = title;
        this.pages = pages;
        this.content = content;
    }
}