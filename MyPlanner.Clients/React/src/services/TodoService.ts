import TodoFolder from "../entities/TodoFolder";
import TodoList from "../entities/TodoList";
import TodoTask from "../entities/TodoTask";
import UpdateTask from "../entities/UpdateTask";

export default class TodoService{
    private _baseUrl:string = "https://myplannerapi.azurewebsites.net/api/todo/"
  
    public async getFolders(): Promise<TodoFolder[]>{
      const res = await this.getResource("folders");
      return res.map((folder : any) => this.remapFolder(folder));
    }

    public async getFolder(id: string): Promise<TodoFolder|undefined>{
      const res = await this.getResource(`folders/${id}`);
      if(res !== undefined)
        return this.remapFolder(res)
      return undefined;
    }

    public async getList(id: string): Promise<TodoList|undefined>{
      const res = await this.getResource(`lists/${id}`);
      if(res !== undefined)
        return this.remapList(res)
      return undefined;
    }

    public async getTask(id: string): Promise<TodoTask|undefined>{
      const res = await this.getResource(`tasks/${id}`);
      if(res !== undefined)
        return this.remapTask(res)
      return undefined;
    }

    public async createFolder(title: string): Promise<string>{
      const res = await this.sendPostRequest("folders",{title: title});
      return res;
    }

    public async deleteFolder(id: string): Promise<boolean>{
      const res = await this.sendDeleteRequest(`folders/${id}`);
      return res;
    }

    public async createList(title: string, folderId: string|null): Promise<string>{
      const res = await this.sendPostRequest("lists",{title: title, folderId: folderId});
      return res;
    }

    public async deleteList(id: string): Promise<boolean>{
      const res = await this.sendDeleteRequest(`lists/${id}`);
      return res;
    }

    public async createTask(title: string, listId: string): Promise<string>{
      const res = await this.sendPostRequest("tasks",{title: title, listId: listId});
      return res;
    }

    public async deleteTask(id: string): Promise<boolean>{
      const res = await this.sendDeleteRequest(`tasks/${id}`);
      return res;
    }

    public async updateTask(updateTask: UpdateTask): Promise<string>{
      const res = await this.sendPutRequest("tasks", updateTask);
      return res;
    }

    private remapFolder(folder: any): TodoFolder{
        return new TodoFolder(folder.id, folder.title, folder.lists);
    }
    private remapList(list: any): TodoList{
      return new TodoList(list.id, list.title, list.tasks);
    }
    private remapTask(task: any): TodoTask{
      return new TodoTask(task.id, task.title, task.listId, task.description, task.isComplete);
    }

    private async getResource(url:string) : Promise<any> {
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

    private async sendPostRequest(url:string, object: any) : Promise<any>{
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

    private async sendDeleteRequest(url:string) : Promise<boolean>{
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

    private async sendPutRequest(url:string, object: any) : Promise<any>{
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
    
        return await response.json();
      } catch (error) {
        if (error instanceof Error)
          console.error(`Error: ${error.message}`);
      }
    }
}