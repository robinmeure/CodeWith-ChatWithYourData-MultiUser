export interface IDocument {
    id: string;
    threadId: string;
    userId: string;
    documentName: string;
    fileSize: number;
    uploadDate: string;
    deleted: boolean;
    availableInSearchIndex: boolean;
    chunkId: string | null;
}