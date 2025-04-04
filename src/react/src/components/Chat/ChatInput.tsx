import { makeStyles, tokens, Button, Badge, Textarea, Dialog, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { BroomRegular, Send24Regular, AttachRegular, Prompt20Regular, DismissRegular } from '@fluentui/react-icons';
import { PredefinedPrompt } from '../../models/Settings';
import { useEffect, useState, useRef } from 'react';

const useClasses = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        width: '70%',
        margin: 'auto',
        marginBottom: tokens.spacingVerticalL,
        alignItems: 'center',
        gap: tokens.spacingVerticalS,
        paddingTop: tokens.spacingVerticalL,
        '@media (max-width: 768px)': {
            width: '90%',
        },
    },
    inputContainer: {
        display: 'flex',
        width: '100%',
        flexDirection: 'column',
        gap: tokens.spacingHorizontalS,
    },
    inputWrapper: {
        display: 'flex',
        position: 'relative',
        width: '100%',
        flexDirection: 'column',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    input: {
        flexGrow: 1,
        width: '100%',
        border: 'none',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: 'transparent',
        padding: '12px 12px 40px 12px', // Extra padding at bottom for buttons
        resize: 'none',
        minHeight: '36px'        
    },
    inputFooter: {
        position: 'absolute',
        bottom: '4px',
        left: '0',
        right: '0',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '0 8px',
        height: '36px',
    },
    leftButtons: {
        display: 'flex',
        gap: '4px',
        alignItems: 'center',
    },
    rightButtons: {
        display: 'flex',
        gap: '4px',
    },
    promptsDropdown: {
        display: 'inline-flex',
    },
    actionButton: {
        height: '28px',
        padding: '4px',
        minWidth: 'auto',
        width: '28px',
    },
    sendButton: {
        height: '28px',
        padding: '4px',
        minWidth: 'auto',
        width: '28px',
        
    },
    modalTitle: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
        borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    modalCloseButton: {
        padding: '4px',
        minWidth: 'auto',
        height: '28px',
        width: '28px',
    },
    promptsModal: {
        maxWidth: '600px',
        width: '90%',
    },
    promptsList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        margin: 0,
        padding: 0,
        listStyle: 'none',
    },
    promptItem: {
        cursor: 'pointer',
        padding: tokens.spacingVerticalS,
        borderRadius: tokens.borderRadiusMedium,
        transition: 'all 0.1s ease',
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
        }
    },
    promptName: {
        fontWeight: tokens.fontWeightSemibold,
        marginBottom: tokens.spacingVerticalXS,
    },
    promptText: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        display: '-webkit-box',
        WebkitLineClamp: 2,
        WebkitBoxOrient: 'vertical',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    }
});

type chatInputType = {
    value: string,
    setValue: (value: string ) => void,
    onSubmit: () => void,
    clearChat: () => void,
    predefinedPrompts?: PredefinedPrompt[]
}

export function ChatInput({ value, setValue, onSubmit, clearChat, predefinedPrompts = [] }: chatInputType) {
    const classes = useClasses();
    const [availablePrompts, setAvailablePrompts] = useState<PredefinedPrompt[]>([]);
    const [showPrompts, setShowPrompts] = useState(false);
    const [rows, setRows] = useState(1);
    const textAreaRef = useRef<HTMLTextAreaElement>(null);
    const [isPromptsModalOpen, setIsPromptsModalOpen] = useState(false);

    
    useEffect(() => {
        setAvailablePrompts(Array.isArray(predefinedPrompts) ? predefinedPrompts : []);
    }, [predefinedPrompts]);
    
    useEffect(() => {
        // Auto-adjust textarea rows based on content
        if (textAreaRef.current) {
            const textArea = textAreaRef.current;
            textArea.style.height = 'auto';
            
            const newRows = Math.min(
                Math.max(Math.ceil(textArea.scrollHeight / 24), 1), // Approximate line height is 24px
                5 // Maximum 5 rows
            );
            
            setRows(newRows);
        }
    }, [value]);
    
    const handleKeyDown = (event: React.KeyboardEvent<HTMLTextAreaElement>) => {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            if (value.trim()) {
                onSubmit();
            }
        }
    };

    const handlePromptClick = (promptText: string) => {
        setValue(promptText);
        if (textAreaRef.current) {
            textAreaRef.current.focus();
        }
    };

    const handlePromptSelect = (promptText: string) => {
        setValue(promptText);
        setIsPromptsModalOpen(false);
        if (textAreaRef.current) {
            textAreaRef.current.focus();
        }
    };

    return (
            <div className={classes.container}>
                <div className={classes.inputContainer}>
                    <div className={classes.inputWrapper}>
                        <Textarea
                            ref={textAreaRef}
                            className={classes.input}
                            value={value}
                            onChange={(_e, data) => setValue(data.value)}
                            onKeyDown={handleKeyDown}
                            placeholder="Ask a question..."
                            resize="none"
                            rows={rows}
                        />
                        
                        <div className={classes.inputFooter}>
                            <div className={classes.leftButtons}>
                                <Button 
                                    className={classes.actionButton} 
                                    icon={<BroomRegular />} 
                                    onClick={clearChat} 
                                    aria-label="Clear session" 
                                    title="Clear session"
                                    appearance="subtle"
                                    size="small"
                                />
                                 
                               {availablePrompts.length > 0 && (
                                <Button
                                    className={classes.actionButton}
                                    icon={<Prompt20Regular />}
                                    onClick={() => setIsPromptsModalOpen(true)}
                                    aria-label="Predefined prompts"
                                    title="Predefined prompts"
                                    appearance="subtle"
                                    size="small"
                                />
                            )}                                                                
                            </div>
                            
                            <div className={classes.rightButtons}>
                                <Button 
                                    className={classes.sendButton} 
                                    onClick={onSubmit} 
                                    icon={<Send24Regular />}
                                    disabled={!value.trim()}
                                    title="Send message"
                                    appearance={value.trim() ? "primary" : "subtle"}
                                    size="small"
                                />
                            </div>
                        </div>
                    </div>
                </div>
                 {/* Prompts Modal Dialog */}
                    <Dialog 
                        open={isPromptsModalOpen} 
                        onOpenChange={(_, data) => setIsPromptsModalOpen(data.open)}
                    >
                        <DialogSurface className={classes.promptsModal}>
                            <div className={classes.modalTitle}>
                                <DialogTitle>Select a prompt</DialogTitle>
                                <Button
                                    className={classes.modalCloseButton}
                                    icon={<DismissRegular />}
                                    onClick={() => setIsPromptsModalOpen(false)}
                                    appearance="subtle"
                                    size="small"
                                />
                            </div>
                            
                            <DialogBody>
                                <DialogContent>
                                    <ul className={classes.promptsList}>
                                        {availablePrompts.map((prompt) => (
                                            <li 
                                                key={prompt.id}
                                                className={classes.promptItem}
                                                onClick={() => handlePromptSelect(prompt.prompt)}
                                            >
                                                <div className={classes.promptName}>{prompt.name}</div>
                                                <div className={classes.promptText}>{prompt.prompt}</div>
                                            </li>
                                        ))}
                                    </ul>
                                </DialogContent>
                            </DialogBody>
                        </DialogSurface>
                    </Dialog>
            </div>

            
        );
    };