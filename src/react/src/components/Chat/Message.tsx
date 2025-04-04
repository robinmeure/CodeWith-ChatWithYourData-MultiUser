import { Field, makeStyles, ProgressBar, tokens, Button, Badge, Divider } from '@fluentui/react-components';
import { Stack, Text } from '@fluentui/react'
import { IChatMessage } from '../../models/ChatMessage';
import React from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { useTextStyles } from '../../styles/sharedStyles';

const useClasses = makeStyles({
    userContainer: {
        display: 'flex',
        justifyContent: 'flex-end',
        marginTop: tokens.spacingVerticalL,
        maxWidth: '90%',
        marginLeft: 'auto',
        '@media (max-width: 599px)': {
            maxWidth: '95%',
        },
    },
    assistantContainer: {
        display: 'flex',
        justifyContent: 'start',
        marginTop: tokens.spacingVerticalL,
        maxWidth: '90%',
        marginRight: 'auto',
        '@media (max-width: 599px)': {
            maxWidth: '95%',
        },
    },
    userTextContainer: {
        backgroundColor: tokens.colorBrandBackground,
        color: tokens.colorNeutralForegroundOnBrand,
        borderRadius: tokens.borderRadiusXLarge,
        maxWidth: '100%',
        padding: tokens.spacingHorizontalL,
        paddingTop: tokens.spacingVerticalM,
        paddingBottom: tokens.spacingVerticalM,
        boxShadow: tokens.shadow4,
        transition: 'all 0.2s ease',
        '&:hover': {
            boxShadow: tokens.shadow8,
        }
    },
    assistantTextContainer: {
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusXLarge,
        maxWidth: '100%',
        padding: tokens.spacingHorizontalL,
        paddingTop: tokens.spacingVerticalM,
        paddingBottom: tokens.spacingVerticalM,
        boxShadow: tokens.shadow4,
        transition: 'all 0.2s ease',
        '&:hover': {
            boxShadow: tokens.shadow8,
        }
    },
    roleLabel: {
        fontSize: tokens.fontSizeBase100,
        marginBottom: tokens.spacingVerticalXS,
    },
    subheader: {
        marginTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalXS,
        fontWeight: tokens.fontWeightRegular,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3
    },
    thinkingContainer: {
        width: '50%',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusXLarge,
        padding: tokens.spacingHorizontalL,
    },
    title: {
        flexGrow: 1,
        fontSize: tokens.fontSizeBase500,
    },
    followUpContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-end',
        marginTop: tokens.spacingVerticalM,
        marginLeft: 'auto',
        marginRight: 'auto',
        maxWidth: '80%',
        gap: tokens.spacingVerticalS,
    },
    followUpButton: {
        transition: 'transform 0.2s ease, background 0.2s ease',
        
    },
    markdownContent: {
        width: '100%',
        '& table': {
            borderCollapse: 'collapse',
            margin: '10px 0',
            width: '100%',
            border: `1px solid ${tokens.colorNeutralStroke1}`,
            tableLayout: 'fixed',
        },
        '& thead': {
            backgroundColor: tokens.colorNeutralBackground3,
        },
        '& th, & td': {
            border: `1px solid ${tokens.colorNeutralStroke1}`,
            padding: '8px 12px',
            textAlign: 'left',
            wordWrap: 'break-word',
        },
        '& th': {
            backgroundColor: tokens.colorNeutralBackground4,
            fontWeight: tokens.fontWeightSemibold,
        },
        '& tr:nth-child(even)': {
            backgroundColor: tokens.colorNeutralBackground2,
        },
        '& pre': {
            backgroundColor: tokens.colorNeutralBackground3,
            padding: '10px',
            borderRadius: '4px',
            overflowX: 'auto',
        },
        '& code': {
            fontFamily: 'monospace',
            backgroundColor: tokens.colorNeutralBackground3,
            padding: '2px 4px',
            borderRadius: '3px',
        },
        '& a': {
            color: tokens.colorBrandForeground1,
            textDecoration: 'none',
            '&:hover': {
                textDecoration: 'underline',
            },
        },
        '& img': {
            maxWidth: '100%',
            height: 'auto',
            borderRadius: tokens.borderRadiusMedium,
        },
        '& blockquote': {
            borderLeft: `4px solid ${tokens.colorBrandStroke1}`,
            padding: '0 16px',
            margin: '8px 0',
            color: tokens.colorNeutralForeground2,
        },
    },
    citationBadge: {
        cursor: 'pointer',
        marginRight: tokens.spacingHorizontalXS,
    },
    citationsContainer: {
        marginTop: tokens.spacingVerticalS,
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalXS,
    }
});

// Custom renderer components for Markdown with improved table rendering
const MarkdownComponents = {
    table: (props: any) => (
        <div style={{ overflowX: 'auto', width: '100%', marginBottom: '16px' }}>
            <table style={{ 
                borderCollapse: 'collapse', 
                width: '100%', 
                border: '1px solid #ddd',
                tableLayout: 'fixed'
            }}>
                {props.children}
            </table>
        </div>
    ),
    thead: (props: any) => (
        <thead style={{ backgroundColor: '#f0f0f0' }}>
            {props.children}
        </thead>
    ),
    tr: (props: any) => (
        <tr style={{ borderBottom: '1px solid #ddd' }}>
            {props.children}
        </tr>
    ),
    th: (props: any) => (
        <th style={{ 
            padding: '10px 8px', 
            textAlign: 'left', 
            borderRight: '1px solid #ddd', 
            borderBottom: '2px solid #ddd',
            backgroundColor: '#f0f0f0',
            fontWeight: 'bold',
            wordWrap: 'break-word'
        }}>
            {props.children}
        </th>
    ),
    td: (props: any) => (
        <td style={{ 
            padding: '8px', 
            textAlign: 'left', 
            borderRight: '1px solid #ddd',
            wordWrap: 'break-word'
        }}>
            {props.children}
        </td>
    ),
};

type messageProps = {
    message: IChatMessage;
    onFollowUp: (question: string) => void;
}

export function Message({ message, onFollowUp }: messageProps) {
    const classes = useClasses();
    const textStyles = useTextStyles();

    const formatCitationLabel = (index: number) => {
        return `[${index + 1}]`;
    };

    const formatMessageDate = (dateString: string | Date | undefined) => {
        if (!dateString) return '';
        
        const messageDate = new Date(dateString);
        const now = new Date();
        
        // Check if valid date
        if (isNaN(messageDate.getTime())) return '';
        
        const isToday = messageDate.toDateString() === now.toDateString();
        const isYesterday = new Date(now.setDate(now.getDate() - 1)).toDateString() === messageDate.toDateString();
        
        const timeOptions: Intl.DateTimeFormatOptions = { hour: '2-digit', minute: '2-digit' };
        const time = messageDate.toLocaleTimeString(undefined, timeOptions);
        
        if (isToday) {
            return ` • Today at ${time}`;
        } else if (isYesterday) {
            return ` • Yesterday at ${time}`;
        } else {
            return ` • ${messageDate.toLocaleDateString()} at ${time}`;
        }
    };

    return (
        <>
            <div id={message.id} className={message.role == "user" ? classes.userContainer : classes.assistantContainer}>
                {message.content == "" ? (
                    <div className={classes.thinkingContainer}>
                        <Field validationMessage="Thinking..." validationState="none">
                            <ProgressBar />
                        </Field>
                    </div>
                ) : (
                    <div className={message.role == "user" ? classes.userTextContainer : classes.assistantTextContainer}>
                        <div className={classes.roleLabel}>
                            {message.role === "user" ? "You" : "AI Assistant"}
                            {formatMessageDate(message.created)}
                        </div>
                       
                        <div className={classes.markdownContent}>
                            <ReactMarkdown 
                                remarkPlugins={[remarkGfm]} 
                                components={MarkdownComponents}
                            >
                                {message.content}
                            </ReactMarkdown>
                        </div>
                        {!!message.context?.citations?.length && (
                            <div className={classes.citationsContainer}>
                                {message.context.citations.map((citation, index) => (
                                    <Badge 
                                        key={`citation-${index}`}
                                        className={classes.citationBadge}
                                        appearance="outline"
                                        title={citation.title || citation.filepath || 'Citation'}
                                    >
                                        {formatCitationLabel(index)}
                                    </Badge>
                                ))}
                            </div>
                        )}
                    </div>
                )}
            </div>
            
            {message.context?.followup_questions && message.context.followup_questions.length > 0 && (
                <div className={classes.followUpContainer}>
                    {message.context.followup_questions.map((question, index) => (
                        <Button 
                            key={index} 
                            className={classes.followUpButton} 
                            onClick={() => onFollowUp(question)}
                            appearance="secondary"
                            shape="rounded"
                        >
                            {question}
                        </Button>
                    ))}
                </div>
            )}
        </>
    );
};
