import { Text, Button, makeStyles, tokens, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { ListItem } from './ListItem';
import { Add24Regular, Home24Regular } from '@fluentui/react-icons';
import { IChat } from '../../models/Chat';

const useClasses = makeStyles({
    root: {
        display: 'flex',
        width: '280px',
        paddingTop: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingVerticalM,
        paddingLeft: tokens.spacingVerticalM,
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground2
    },
    headerContainer: {
        height: "48px",
        display: "flex",
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between'
    },
    listHeaderText: {
        marginTop: tokens.spacingVerticalM,
        marginBottom: tokens.spacingVerticalM,
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase300
    }
});

type chatListType = {
    chats: IChat[] | undefined,
    selectedChatId: string | undefined,
    selectChat: (chatId?: string) => void,
    addChat: (userId: string) => Promise<void>,
    deleteChat: (chatId: string) => Promise<void>,
    loading: boolean
}

export function Chatlist({ chats, selectedChatId, selectChat, addChat, deleteChat, loading }: chatListType) {
    const classes = useClasses();

    return (
        <div className={classes.root}>
            <div className={classes.headerContainer}>
                <Button onClick={() => selectChat()} size="large" icon={<Home24Regular />} />
                <Button onClick={() => addChat("demouser")} size="large" icon={<Add24Regular />} />
            </div>
            <Text className={classes.listHeaderText}>My chats</Text>

            {loading && (
                <Skeleton aria-label="Loading Content">
                    <SkeletonItem size={64} shape="rectangle" />
                </Skeleton>
            )}
            {/* Chat list */}
            {(chats && !loading) && chats.map((item) => {
                return <ListItem item={item} isSelected={selectedChatId == item.id} selectChat={selectChat} deleteChat={deleteChat} />
            })}
        </div>
    );
};