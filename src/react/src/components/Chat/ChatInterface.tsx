import { makeStyles, Toast, Toaster, ToastTitle, tokens, useId, useToastController } from '@fluentui/react-components';
import { useChatMessages } from '../../hooks/useChatMessages';
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
    },
    toast: {
        width: '200%'
    }
});

type chatInterfaceType = {
    selectedChatId: string | undefined
}

export function ChatInterface({ selectedChatId }: chatInterfaceType) {
    const classes = useClasses();
    const { messages, sendMessage, chatPending } = useChatMessages(selectedChatId);
    const [userInput, setUserInput] = useState<string>("");
    const [selectedTab, setSelectedTab] = useState<string>("chat");
    const toasterId = useId("toaster");
    const { dispatchToast } = useToastController(toasterId);

    const notify = () =>
        dispatchToast(
            <Toast className={classes.toast}>
                <ToastTitle>Your rate limit for sending messages is exceeded, please try again in a while.</ToastTitle>
            </Toast>,
            { position: "bottom", intent: "warning", timeout: 5000 }
        );

    const submitMessage = async (message: string) => {
        if (selectedChatId && message) {
            setUserInput("");
            const success = await sendMessage({ message });
            if (!success) notify();
        }
    }

    const handleFollowUp = (question: string) => {
        submitMessage(question);
    }

    return (
        <div className={classes.root}>
            <div className={classes.body}>
                <Toaster toasterId={toasterId} />
                {(selectedChatId) && (<ChatHeader selectedTab={selectedTab} setSelectedTab={setSelectedTab} />)}
                {(selectedTab === "chat" && selectedChatId) && (
                    <>
                        <MessageList messages={messages} loading={chatPending} onFollowUp={handleFollowUp} />
                        <ChatInput value={userInput} setValue={setUserInput} onSubmit={() => submitMessage(userInput)} />
                    </>
                )}
                {(selectedTab === "documents" && selectedChatId) && (<DocumentViewer chatId={selectedChatId} />)}
            </div>
        </div>
    );
};
