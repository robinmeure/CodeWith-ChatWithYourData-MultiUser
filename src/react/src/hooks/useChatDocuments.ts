import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { IDocument } from "../models/Document";
import { DocumentService } from "../services/DocumentService";
import { useAuth } from "./useAuth";

export const useChatDocuments = (chatId: string | undefined) => {

    const queryClient = useQueryClient();
    const documentService = new DocumentService();
    const { accessToken } = useAuth();   


    const [documents, setDocuments] = useState<IDocument[]>([]);

    const { isPending: documentsPending, error: documentsError, data: documentData } = useQuery({
        queryKey: ['documents', chatId],
        queryFn: async () => documentService.getDocumentsAsync(chatId || "", accessToken),
        enabled: chatId != undefined && accessToken != undefined && accessToken != ""
    });

    useEffect(() => {
        if (documentData) {
            setDocuments(documentData);
        }
    }, [ documentData]);

    const { mutateAsync: addDocuments} = useMutation({
        mutationFn: ({chatId, documents} : {chatId: string, documents: File[]}) => documentService.addDocumentsAsync({chatId, documents, token: accessToken}),
        onError: () => {
            console.log('Failed to upload a document.');
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['documents', chatId] });
        }
    });

    const { mutateAsync: deleteDocument } = useMutation({
        mutationFn: ({chatId, documentId} : {chatId: string, documentId: string}) => documentService.deleteDocumentAsync({chatId, documentId, token: accessToken}),
        onError: () => {
            console.log('Failed to delete a document.');
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['documents', chatId] });
        }
    });

    return {
        documentsPending,
        documentsError,
        documents,
        addDocuments,
        deleteDocument
    };

}