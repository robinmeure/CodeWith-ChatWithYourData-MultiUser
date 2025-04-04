import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { ChatService } from "../services/ChatService";
import { IChatMessage } from "../models/ChatMessage";
import { useAuth } from "./useAuth";
import readNDJSONStream from "ndjson-readablestream";

export const useChatMessages = (chatId: string | undefined) => {
    const chatService = new ChatService();
    const { userId, accessToken } = useAuth();   

    const [messages, setMessages] = useState<IChatMessage[]>([]);

    const { isPending: chatPending, error: chatError, data: messagesResult } = useQuery({
        queryKey: ['chat', chatId],
        queryFn: async () => chatService.getChatMessagesAsync({ chatId: chatId || "", token: accessToken }),
        enabled: userId != undefined && accessToken != undefined && accessToken != "" && chatId != "" && chatId != undefined,
        staleTime: 10000
    });

    useEffect(() => {
        if (messagesResult) {
            setMessages(messagesResult.filter(message => message.role !== 'system'));
        }
    }, [messagesResult]);

    // Existing sendMessage implementation
    const sendMessage = async ({ message }: { message: string }) => {
        if (!chatId) return false; 
        let result = '';
        setMessages(prev => {
            const updated = [...prev];
            updated.push(
                {
                    role: 'user',
                    content: message
                },
                {
                    role: 'assistant',
                    content: result
                }
            );
            return updated;
        });

        const response = await chatService.sendMessageAsync({ chatId: chatId, message: message, token: accessToken });
        
        if (!response || !response.body) {
            return false;
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let jsonBuffer = '';
        
        while (true) {
            const { value, done } = await reader.read();
            if (done) {
                break;
            }
            
            const decodedChunk = decoder.decode(value);
            jsonBuffer += decodedChunk;
            
            try {
                // Try to parse complete JSON objects
                const lines = jsonBuffer.split('\n').filter(line => line.trim() !== '');
                
                // Process complete lines
                for (let i = 0; i < lines.length - 1; i++) {
                    try {
                        const chunk = JSON.parse(lines[i]);
                        if (chunk.content) {
                            result += chunk.content;
                            setMessages(prev => {
                                const updated = [...prev];
                                updated[updated.length - 1] = {
                                    role: 'assistant',
                                    content: result,
                                    context: chunk.context,
                                    id: chunk.id,
                                    created: chunk.created
                                };
                                return updated;
                            });
                        }
                    } catch (parseError) {
                        console.error('Error parsing JSON line:', parseError);
                    }
                }
                
                // Keep the last potentially incomplete line in the buffer
                jsonBuffer = lines[lines.length - 1] || '';
            } catch (error) {
                console.error('Error processing chunks:', error);
                // Continue to next chunk, don't break the stream processing
            }
        }
        
        // Try to parse any remaining data in the buffer
        if (jsonBuffer.trim()) {
            try {
                const chunk = JSON.parse(jsonBuffer);
                if (chunk.content) {
                    result += chunk.content;
                    setMessages(prev => {
                        const updated = [...prev];
                        updated[updated.length - 1] = {
                            role: 'assistant',
                            content: result,
                            context: chunk.context,
                            id: chunk.id,
                            created: chunk.created
                        };
                        return updated;
                    });
                }
            } catch (error) {
                console.error('Error parsing final JSON chunk:', error);
            }
        }

        // If message is still empty, remove the two added messages
        if (result === '') {
            setMessages(prev => {
                const updated = [...prev];
                updated.pop();
                updated.pop();
                return updated;
            });
            return false;
        }
        return true;
    };

    // New method to support streaming messages with an onChunk callback
    // The onChunk callback is invoked with each received chunk and
    // a flag indicating whether the stream is complete.
    const sendMessageStream = async ({
        message,
        onChunk,
    }: {
        message: string,
        onChunk: (chunk: IChatMessage, done: boolean) => void
    }) => {
        if (!chatId) return false;
        let result = '';
        // Add initial messages for UI feedback
        setMessages(prev => [
            ...prev,
            { role: 'user', content: message },
            { role: 'assistant', content: '' }
        ]);

        const response = await chatService.sendMessageAsync({ chatId: chatId, message: message, token: accessToken });
        if (!response || !response.body) {
            return false;
        }

        while (true) {
            for await (const event of readNDJSONStream(response.body)) {
                if (event["content"] != undefined) {
                   result = event["content"];
                    setMessages(prev => {
                        const updated = [...prev];
                        updated[updated.length - 1] = {
                            role: 'assistant',
                            content: result,
                            context: event.context,
                            id: event.id,
                            created: event.created
                        };
                        return updated;
                    });
                } else if (event["delta"] && event["delta"]["content"]) {
                    // setIsLoading(false);
                    // await updateState(event["delta"]["content"]);
                } else if (event["context"]) {
                    // Update context with new keys from latest event
                    onChunk(
                        {
                            role: 'assistant',
                            content: result,
                            context: event.context,
                            id: event.id,
                            created: event.created
                        },
                        false
                    );
                } 
            }

        }
        
        // Remove messages if no content received
        if (result === '') {
            setMessages(prev => prev.slice(0, -2));
            return false;
        }
        return true;
    };

    const deleteMessages = async () => {
        if (!chatId) return false; 
        const response = await chatService.deleteMessagesAsync({ chatId: chatId, token: accessToken });
        if (!response) {
            return false;
        }
        setMessages([]);
        return true;
    };

    return {
        chatPending,
        chatError,
        messages,
        sendMessage,
        sendMessageStream,
        deleteMessages  
    };
};