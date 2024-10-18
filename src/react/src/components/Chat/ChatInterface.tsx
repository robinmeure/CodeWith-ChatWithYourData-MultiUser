import { makeStyles, shorthands, tokens, TabList, Tab, Subtitle2 } from '@fluentui/react-components';
import { useChat } from '../../hooks/useChat';
import { useState } from 'react';
import { MessageList } from './MessageList';
import { ChatInput } from './ChatInput';
import { DocumentViewer } from '../Documents/DocumentViewer';

const useClasses = makeStyles({
    root: {
        display: 'flex',
        width: "100%",
        paddingTop: tokens.spacingHorizontalM,
        flexDirection: 'column',
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
        height: 'calc(100vh - 60px)',
        flexDirection: 'column',
    },
    chat: {
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        paddingLeft: tokens.spacingVerticalL,
        paddingRight: tokens.spacingVerticalL,
    },
    subheader: {
        marginTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalXS,
        fontWeight: tokens.fontWeightRegular,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3
    },
    title: {
        flexGrow: 1,
        fontSize: tokens.fontSizeBase500,
    },

    input: {
        flexGrow: 1,
        ...shorthands.padding(tokens.spacingHorizontalNone),
        ...shorthands.border(tokens.borderRadiusNone),
        backgroundColor: tokens.colorSubtleBackground,
        fontSize: tokens.fontSizeBase500,
    },
});

type chatInterfaceType = {
    selectedChatId: string | undefined
}

export function ChatInterface({ selectedChatId }: chatInterfaceType) {
    const classes = useClasses();
    const { chat, messages, sendMessage } = useChat(selectedChatId);
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
            {chat ? (
                <>
                    <div className={classes.header}>
                        <TabList selectedValue={selectedTab} onTabSelect={(_e, data) => { setSelectedTab(data.value as string) }}>
                            <Tab value="chat">Chat</Tab>
                            <Tab value="documents">Documents</Tab>
                        </TabList>
                    </div>

                    <div className={classes.body}>
                        {selectedTab === "chat" ? (
                            <>
                                <MessageList messages={messages} />
                                <ChatInput value={userInput} setValue={setUserInput} onSubmit={submitMessage} />
                            </>
                        ) : (
                            <>
                                <DocumentViewer chatId={chat.id} />
                            </>
                        )}

                    </div>
                </>
            ) : (
                <div className={classes.header}>
                    <Subtitle2>Chat on your documents</Subtitle2>
                </div>
            )}
        </div>
    );
};