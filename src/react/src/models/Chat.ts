import { IChatMessage } from "./ChatMessage";

export interface IChat {
    id: string, 
    name: string,
    userId?: string,
    timestamp?: string,
    messages?: IChatMessage[]
}