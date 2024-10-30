import { makeStyles } from '@fluentui/react-components';
import { Message } from './Message';
import { useEffect, useRef } from 'react';
import { IChatMessage } from '../../models/ChatMessage';
import { ChatSkeleton } from '../Loading/ChatSkeleton';

const useClasses = makeStyles({
    scrollContainer: {
        flex: 1,
        heigth: '100%',
        display: 'flex',
        overflow: 'scroll',
        overflowX: 'hidden',
        flexDirection: 'column',
        '&::-webkit-scrollbar': {
            display: 'none'
        },
    },
    messageContainer: {
        width: '70%',
        margin: 'auto',
        height: 'calc(100vh - 60px)',
    }
});

type messageListType = {
    messages: IChatMessage[],
    loading: boolean
}

export function MessageList({ messages, loading }: messageListType) {

    const classes = useClasses();
    const containerRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (containerRef.current ) {
            const newMessageHeight = 100;
            containerRef.current.scrollTo({
                top: containerRef.current.scrollHeight - containerRef.current.clientHeight + newMessageHeight,
                behavior: 'smooth',
            });
        }
    }, [messages]);

    return (
        <div ref={containerRef} className={classes.scrollContainer}>
            <div className={classes.messageContainer}>
                {loading ? (<ChatSkeleton />) : (
                    <>
                        {messages &&
                            messages.map((item: IChatMessage) => {
                                return <Message message={item} />
                            })}</>
                )}
            </div>
        </div>
    );
};