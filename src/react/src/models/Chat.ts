import { IChatMessage } from "./ChatMessage";

export interface IChat {
    id: string, 
    threadName: string,
    userId?: string,
    timestamp?: string,
    messages?: IChatMessage[]
}