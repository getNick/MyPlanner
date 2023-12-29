import TodoFolder from "../entities/TodoFolder";

export default class TodoService{
    private _baseUrl:string = "https://myplannerapi.azurewebsites.net/api/todo/"
  
    public async getFolders(): Promise<TodoFolder[]>{
      const res = await this.getResource("folders");
      return res.map((folder : any) => this.remapFolder(folder));
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

    private remapFolder(folder: any): TodoFolder{
        return new TodoFolder(folder.id, folder.title, folder.lists);
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
}