import { makeStyles, tokens } from '@fluentui/react-components';
import { useChat } from '../../hooks/useChat';
import { useState } from 'react';
import { MessageList } from './MessageList';
import { ChatInput } from './ChatInput';
import { DocumentViewer } from '../Documents/DocumentViewer';
import { ChatHeader } from './ChatHeader';

const useClasses = makeStyles({
    root: {
        display: 'flex',
        width: "100%",
        paddingTop: tokens.spacingHorizontalM,
        flexDirection: 'column'
    },
    header: {
        height: "48px",
        display: "flex",
        flexDirection: 'column',
        paddingLeft: tokens.spacingVerticalM,
        paddingRight: tokens.spacingVerticalM,
        justifyContent: "center"
    },
    body: {
        display: 'flex',
        height: '100%',
        flexDirection: 'column',
    }
});

type chatInterfaceType = {
    selectedChatId: string | undefined
}

export function ChatInterface({ selectedChatId }: chatInterfaceType) {
    const classes = useClasses();
    const { chat, messages, sendMessage, chatPending } = useChat(selectedChatId);
    const [userInput, setUserInput] = useState<string>("");
    const [selectedTab, setSelectedTab] = useState<string>("chat");


    const submitMessage = () => {
        if (chat && userInput) {
            setUserInput("");
            sendMessage({ message: userInput });
        }
    }

    return (
        <div className={classes.root}>
            <div className={classes.body}>
                {(selectedChatId) && (<ChatHeader selectedTab={selectedTab} setSelectedTab={setSelectedTab} />)}
                {(selectedTab === "chat" && selectedChatId) && (
                    <>
                        <MessageList messages={messages} loading={chatPending} />
                        <ChatInput value={userInput} setValue={setUserInput} onSubmit={submitMessage} />
                    </>
                )}
                {(selectedTab === "documents" && selectedChatId) && (<DocumentViewer chatId={chat?.id} />)}
            </div>
        </div>
    );
};