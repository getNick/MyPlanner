import Page from "../entities/Pages/Page";
import TodoList from "../entities/TodoList/TodoList";
import TodoTask from "../entities/TodoList/TodoTask";
import UpdatePage from "../entities/Pages/UpdatePage";
import UpdateTask from "../entities/TodoList/UpdateTask";
import CreatePage from "../entities/Pages/CreatePage";
import PageContent from "../entities/Pages/PageContent";
import SharePage from "../entities/Pages/SharePage";
import Note from "../entities/Note/Note";
import UpdateNote from "../entities/Note/UpdateNote";
import StartTaskSession from "../entities/TodoList/StartTaskSession";
import StopTaskSession from "../entities/TodoList/StopTaskSession";

export default class TodoService {
  private _baseUrl: string | undefined = process.env.REACT_APP_API_URL ?? 'http://localhost:5206/api/';
  private getToken: () => Promise<string | null>;

  constructor(getToken: () => Promise<string | null>) {
    this.getToken = getToken;
  }

  // Pages  
  public async getPages(): Promise<Page[]> {
    const uri = `pages`;
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

  public async getPageContent(id: string): Promise<TodoList | Note | undefined> {
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

  public async sharePage(sharePage: SharePage): Promise<boolean> {
    const res = await this.sendPostRequest(`pages/share`, sharePage);
    return res;
  }

  //TodoList
  public async getTodoList(id: string): Promise<TodoList | undefined> {
    const content = this.getPageContent(id);
    return content as unknown as TodoList;
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

  public async startTaskSession(startTaskSession: StartTaskSession): Promise<boolean> {
    const res = await this.sendPostRequest(`todo/tasks/${startTaskSession.taskId}/sessions/start`, startTaskSession);
    return res;
  }

  public async stopTaskSession(stopTaskSession: StopTaskSession): Promise<boolean> {
    const res = await this.sendPostRequest(`todo/tasks/${stopTaskSession.taskId}/sessions/stop`, stopTaskSession);
    return res;
  }

  // Note
  public async getNote(id: string): Promise<Note | undefined> {
    const content = this.getPageContent(id);
    return content as unknown as Note;
  }

  public async updateNote(updateNote: UpdateNote): Promise<boolean> {
    const res = await this.sendPutRequest("note", updateNote);
    return res;
  }


  private remapPage(folder: any): Page {
    const content = folder.content !== null ? new PageContent(folder.content.id, folder.content.type) : undefined;
    const pages: Page[] = [];
    if (folder.includePages !== undefined && Array.isArray(folder.includePages)) {
      folder.includePages.forEach((x: any) => {
        pages.push(this.remapPage(x))
      });
    }
    return new Page(folder.id, folder.title, pages, content);
  }
  private remapContent(content: any): TodoList | Note | undefined {
    if (content.type === "TodoList") {
      const tasks = content.tasks && Array.isArray(content.tasks)
        ? content.tasks.map((task: any) => this.remapTask(task))
        : [];
      return new TodoList(content.id, tasks);
    }
    if (content.type === "Note") {
      return new Note(content.id, content.content);
    }
    return undefined;
  }

  private remapTask(task: any): TodoTask {
    return new TodoTask(task.id, task.title, task.listId, task.description, task.isComplete, task.startedSessionTimestamp, task.sessions);
  }

  private async getResource(url: string): Promise<any> {
    try {
      const token = await this.getToken();
      const request: string = `${this._baseUrl}${url}`;
      const response = await fetch(request, {
        method: 'GET',
        headers: { 'Authorization': `Bearer ${token}` }
      });

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
      const token = await this.getToken();
      const request: string = `${this._baseUrl}${url}`;
      const response = await fetch(request, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`
        },
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
      const token = await this.getToken();
      const request: string = `${this._baseUrl}${url}`;
      const response = await fetch(request, {
        method: 'Delete',
        headers: { Authorization: `Bearer ${token}` },
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
      const token = await this.getToken();
      const request: string = `${this._baseUrl}${url}`;
      const response = await fetch(request, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`
        },
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