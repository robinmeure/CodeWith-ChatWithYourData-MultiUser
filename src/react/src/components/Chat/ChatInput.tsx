import { makeStyles, tokens, Button, Input } from '@fluentui/react-components';
import { Send24Regular } from '@fluentui/react-icons';

const useClasses = makeStyles({
    container: {
        display: 'flex',
        height: '100px',
        width: '70%',
        margin: 'auto',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        justifyContent: 'space-between',
        paddingTop: tokens.spacingVerticalL,
        paddingBottom: tokens.spacingVerticalL
    },
    input: {
        flexGrow: 1,
        height: '60px',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground2
    },
    button: {
        height: '60px'
    }
});

type chatInputType = {
    value: string,
    setValue: (value: string ) => void,
    onSubmit: () => void
}

export function ChatInput({ value, setValue, onSubmit }: chatInputType) {
    const classes = useClasses();
    
    const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
        if (event.key === 'Enter') {
          onSubmit();
        }
    };

    return (
        <div className={classes.container}>
            <Input onKeyDown={handleKeyDown} className={classes.input} size="large" value={value} onChange={(_e, data) => setValue(data.value)}/>
            <Button className={classes.button} onClick={onSubmit} size="large" icon={<Send24Regular />}/>
        </div>
    );
};