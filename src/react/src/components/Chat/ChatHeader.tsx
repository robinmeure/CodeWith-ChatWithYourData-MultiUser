import { makeStyles, tokens, TabList, Tab } from '@fluentui/react-components';

const useClasses = makeStyles({
    header: {
        height: "48px",
        display: "flex",
        flexDirection: 'column',
        paddingLeft: tokens.spacingVerticalM,
        paddingRight: tokens.spacingVerticalM,
        justifyContent: "center"
    }
});

type chatHeaderType = {
    selectedTab: string,
    setSelectedTab: (tab: string) => void
}

export function ChatHeader({ selectedTab, setSelectedTab }: chatHeaderType) {

    const classes = useClasses();

    return (
        <div className={classes.header}>
            <TabList selectedValue={selectedTab} onTabSelect={(_e, data) => { setSelectedTab(data.value as string) }}>
                <Tab value="chat">Chat</Tab>
                <Tab value="documents">Documents</Tab>
            </TabList>
        </div>
    );
};