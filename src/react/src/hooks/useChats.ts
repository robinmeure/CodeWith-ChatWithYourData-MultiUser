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

    const addMutation = useMutation({
        mutationFn: chatService.createChatAsync,
        onError: () => {
            console.log('Failed to create a chat.');
        },
        onSuccess: (data) => {
            queryClient.invalidateQueries({ queryKey: ['chats'] });
            selectChat(data.id);
        }
    });

    const deleteMutation = useMutation({
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

    const addChat = async (userId: string) => {
        addMutation.mutate(userId);
    };

    const deleteChat = async(chatId: string) => {
        deleteMutation.mutate(chatId);
    }

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