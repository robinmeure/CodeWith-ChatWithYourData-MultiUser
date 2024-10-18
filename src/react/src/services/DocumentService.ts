import { IDocument } from "../models/Document";

export class DocumentService {

    private readonly baseUrl = import.meta.env.VITE_BACKEND_URL;

    public getDocumentsAsync = async (chatId: string): Promise<IDocument[]> => {
        try {
            const response = await fetch(`${this.baseUrl}/chats/${chatId}`);
            if (!response.ok) {
                throw new Error(`Error fetching chat: ${response.statusText}`);
            }
            const jsonResponse = await response.json();
            const documents: IDocument[] = jsonResponse.documents;
            return documents;
        } catch (error) {
            console.error('Failed to fetch chats:', error);
            throw error;
        }
    };

    public addDocumentsAsync = async ({ chatId, documents }: { chatId: string, documents: File[] }): Promise<boolean> => {

        if (!chatId || !Array.isArray(documents) || documents.length === 0) {
            console.log('No chat or documents to upload');
            return false;
        }
        const formData = new FormData();
        documents.forEach(file => {
            formData.append('file', file);
        });

        try {
            const response = await fetch(`${this.baseUrl}/chats/${chatId}/documents`, {
                method: 'POST',
                body: formData,
            });
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