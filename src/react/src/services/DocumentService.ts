import { IDocument } from "../models/Document";

export class DocumentService {

    private readonly baseUrl = import.meta.env.VITE_BACKEND_URL;

    public getDocumentsAsync = async (chatId: string): Promise<IDocument[]> => {
        try {
            const response = await fetch(`${this.baseUrl}/chats/${chatId}/Document`);
            if (!response.ok) {
                throw new Error(`Error fetching chat: ${response.statusText}`);
            }
           
            const documents: IDocument[] = await response.json();
            return documents;
        } catch (error) {
            console.error('Failed to fetch chats:', error);
            throw error;
        }
    };

    public addDocumentsAsync = async ({ chatId, userId, documents }: { chatId: string, userId: string, documents: File[] }): Promise<boolean> => {

        if (!chatId || !Array.isArray(documents) || documents.length === 0) {
            console.log('No chat or documents to upload');
            return false;
        }
        const formData = new FormData();
        documents.forEach(file => {
            formData.append('documents', file);
        });

        console.log("UPLOAD");

        try {
            const response = await fetch(`${this.baseUrl}/chats/${chatId}/Document/upload?userId=${userId}`, {
                method: 'POST',
                body: formData
            });
            console.log(response);
            if (!response.ok) {
                return false;
            }
            return true;
        } catch (e) {
            console.log(e);
            return false;
        }
    }

    public deleteDocumentAsync = async (documentId: string): Promise<boolean> => {
        console.log(documentId)
        // TO BE IMPLEMENTED.
        return true;
    }
}