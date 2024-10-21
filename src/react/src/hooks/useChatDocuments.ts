import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { IDocument } from "../models/Document";
import { DocumentService } from "../services/DocumentService";

export const useChatDocuments = (chatId: string | undefined) => {

    const queryClient = useQueryClient();
    const documentService = new DocumentService();

    const [documents, setDocuments] = useState<IDocument[]>([]);

    const { isPending: documentsPending, error: documentsError, data: documentData } = useQuery({
        queryKey: ['documents', chatId],
        queryFn: async () => documentService.getDocumentsAsync(chatId || ""),
        enabled: chatId != undefined
    });

    useEffect(() => {
        if (chatId) {
            if (documentData) {
                setDocuments(documentData);
            }
        }
    }, [chatId, documentData]);

    const { mutateAsync: addDocuments} = useMutation({
        mutationFn: documentService.addDocumentsAsync,
        onError: () => {
            console.log('Failed to upload a document.');
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['documents', chatId] });
        }
    });

    const { mutateAsync: deleteDocument } = useMutation({
        mutationFn: documentService.deleteDocumentAsync,
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