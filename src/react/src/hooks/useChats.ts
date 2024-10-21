import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { ChatService } from "../services/ChatService";

export const useChats = () => {

    const [selectedChatId, setSelectedChatId] = useState<string | undefined>(undefined);

    const queryClient = useQueryClient();
    const chatService = new ChatService();

    const { isPending, error, data: chats } = useQuery({
        queryKey: ['chats'],
        queryFn: async () => chatService.getChatsAsync()
    });

    const { mutateAsync: addChat} = useMutation({
        mutationFn: chatService.createChatAsync,
        onError: () => {
            console.log('Failed to create a chat.');
        },
        onSuccess: (data) => {
            queryClient.invalidateQueries({ queryKey: ['chats'] });
            selectChat(data.id);
        }
    });

    const { mutateAsync: deleteChat} = useMutation({
        mutationFn: chatService.deleteChatAsync,
        onError: () => {
            console.log('Failed to delete chat.');
        },
        onSuccess: (data, variables) => {
            if(data){
                queryClient.invalidateQueries({ queryKey: ['chats'] });
                if(variables){
                    if(selectedChatId === variables){
                        selectChat();
                    }
                }
            }
        }
    });

    const selectChat = (chatId?: string) => {
        if (chatId) {
            setSelectedChatId(chatId);
        } else {
            setSelectedChatId(undefined);
        }
    };

    return {
        isPending,
        error,
        chats,
        selectChat,
        selectedChatId,
        addChat,
        deleteChat
    }
}