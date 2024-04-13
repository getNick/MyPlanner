import Page from "../entities/Pages/Page";
import TodoList from "../entities/TodoList";
import TodoTask from "../entities/TodoTask";
import UpdatePage from "../entities/Pages/UpdatePage";
import UpdateTask from "../entities/UpdateTask";
import CreatePage from "../entities/Pages/CreatePage";
import PageContent from "../entities/Pages/PageContent";

export default class TodoService {

  private _baseUrl: string = "https://myplannerapi.azurewebsites.net/api/"

  // Pages
  public async getPages(userId: string): Promise<Page[]> {
    const uri = `pages?UserId=${userId}`;
    const res = await this.getResource(uri);
    if (res === undefined)
      return [];
    return res.map((folder: any) => this.remapPage(folder));
  }

  public async getPage(id: string): Promise<Page | undefined> {
    const res = await this.getResource(`pages/${id}`);
    if (res !== undefined)
      return this.remapPage(res)
    return undefined;
  }

  public async getPageContent(id: string): Promise<TodoList | undefined> {
    const res = await this.getResource(`pages/${id}/content`);
    if (res !== undefined)
      return this.remapContent(res)
    return undefined;
  }

  public async createPage(model: CreatePage): Promise<string> {
    const res = await this.sendPostRequest("pages", model);
    return res;
  }

  public async updateFolder(updateFolder: UpdatePage): Promise<boolean> {
    const res = await this.sendPutRequest("pages", updateFolder);
    return res;
  }

  public async deletePage(id: string): Promise<boolean> {
    const res = await this.sendDeleteRequest(`pages/${id}`);
    return res;
  }

  // Tasks
  public async getTask(id: string): Promise<TodoTask | undefined> {
    const res = await this.getResource(`todo/tasks/${id}`);
    if (res !== undefined)
      return this.remapTask(res)
    return undefined;
  }

  public async createTask(title: string, listId: string): Promise<string> {
    const res = await this.sendPostRequest("todo/tasks", { title: title, listId: listId });
    return res;
  }

  public async deleteTask(id: string): Promise<boolean> {
    const res = await this.sendDeleteRequest(`todo/tasks/${id}`);
    return res;
  }

  public async updateTask(updateTask: UpdateTask): Promise<boolean> {
    const res = await this.sendPutRequest("todo/tasks", updateTask);
    return res;
  }

  private remapPage(folder: any): Page {
    const content = folder.content !== null ? new PageContent(folder.content.id, folder.content.type) : undefined;
    return new Page(folder.id, folder.title, folder.includePages, content);
  }
  private remapContent(content: any): TodoList | undefined {
    if (content.type === "TodoList")
      return new TodoList(content.id, content.tasks);
    return undefined;
  }
  private remapTask(task: any): TodoTask {
    return new TodoTask(task.id, task.title, task.listId, task.description, task.isComplete);
  }

  private async getResource(url: string): Promise<any> {
    try {
      const request: string = `${this._baseUrl}${url}`;
      const response = await fetch(request);

      if (!response.ok) {
        throw new Error(`Failed to fetch data from ${url}: ${response.status} ${response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      if (error instanceof Error)
        console.error(`Error: ${error.message}`);
    }
  }

  private async sendPostRequest(url: string, object: any): Promise<any> {
    try {
      const request: string = `${this._baseUrl}${url}`;
      const response = await fetch(request, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(object)
      });

      if (!response.ok) {
        throw new Error(`Failed post request ${url}: ${response.status} ${response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      if (error instanceof Error)
        console.error(`Error: ${error.message}`);
    }
  }

  private async sendDeleteRequest(url: string): Promise<boolean> {
    try {
      const request: string = `${this._baseUrl}${url}`;
      const response = await fetch(request, {
        method: 'Delete',
      });

      if (!response.ok) {
        throw new Error(`Failed delete request ${url}: ${response.status} ${response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      if (error instanceof Error)
        console.error(`Error: ${error.message}`);
    }
    return true;
  }

  private async sendPutRequest(url: string, object: any): Promise<boolean> {
    try {
      const request: string = `${this._baseUrl}${url}`;
      const response = await fetch(request, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(object)
      });

      if (!response.ok) {
        throw new Error(`Failed post request ${url}: ${response.status} ${response.statusText}`);
      }
    } catch (error) {
      if (error instanceof Error)
        console.error(`Error: ${error.message}`);
    }
    return true;
  }
}