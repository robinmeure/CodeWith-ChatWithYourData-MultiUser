import { IChat } from "../models/Chat";

export class ChatService {

    private readonly baseUrl = import.meta.env.VITE_BACKEND_URL;

    public getChatsAsync = async (): Promise<IChat[]> => {
        try {
            const response = await fetch(`${this.baseUrl}/chats`);
            if (!response.ok) {
                throw new Error(`Error fetching chats: ${response.statusText}`);
            }
            const chats: IChat[] = await response.json();
            return chats;
        } catch (error) {
            console.error('Failed to fetch chats:', error);
        }
        return [];
    };

    public getChatAsync = async (chatId: string): Promise<IChat> => {
        try {
            const response = await fetch(`${this.baseUrl}/chats/${chatId}`);
            if (!response.ok) {
                throw new Error(`Error fetching chat: ${response.statusText}`);
            }
            const chat: IChat = await response.json();
            return chat;
        } catch (error) {
            console.error('Failed to fetch chats:', error);
            throw error;
        }
    };

    public createChatAsync = async (userId: string): Promise<IChat> => {

        const body = {
            userId: userId
        }

        try {
            const response = await fetch(`${this.baseUrl}/chats`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(body),
            });
            if (!response.ok) {
                throw new Error(`Error creating chat: ${response.statusText}`);
            }
            const jsonResponse = await response.json();
            const chat: IChat = jsonResponse.item;
            return chat;
        } catch (error) {
            console.error('Failed to create chat:', error);
            throw error; 
        }
    }

    public deleteChatAsync = async (chatId: string): Promise<boolean> => {

        try {
            const response = await fetch(`${this.baseUrl}/chats/${chatId}`, {
                method: 'DELETE',
                headers: {
                    'Content-Type': 'application/json',
                }
            });
            if (!response.ok) {
                throw new Error(`Error deleting chat: ${response.statusText}`);
            }
            return true;
        } catch (error) {
            console.error('Failed to create chat:', error);
            return false;
        }
    }

    public sendMessageAsync = async (chatId: string, message: string): Promise<Response> => {

        try{
            const response = await fetch(`${this.baseUrl}/chats/${chatId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ input: message }),
            });
            return response;
        } catch( error ) {
            console.error('Failed to send message:', error);
            throw error;
        }
    }
}