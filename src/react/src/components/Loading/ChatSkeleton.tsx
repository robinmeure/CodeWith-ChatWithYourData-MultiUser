import { makeStyles, tokens, Skeleton, SkeletonItem } from '@fluentui/react-components';

const useClasses = makeStyles({
    userContainer: {
        display: 'flex',
        justifyContent: 'flex-end',
        marginTop: tokens.spacingVerticalL
    },
    assistantContainer: {
        display: 'flex',
        justifyContent: 'start',
        marginTop: tokens.spacingVerticalL
    },
    skeletonItem: {
        backgroundColor: tokens.colorNeutralBackground1Hover,
        borderRadius: tokens.borderRadiusXLarge,
        maxWidth: '80%',
        padding: tokens.spacingHorizontalM,
        boxShadow: tokens.shadow2
    }
});

export function ChatSkeleton() {
    const classes = useClasses();

    return (
        <Skeleton aria-label="Loading Content">
            <div className={classes.userContainer}>
                <SkeletonItem size={32} shape="rectangle" className={classes.skeletonItem}/>
            </div>
            <div className={classes.assistantContainer}>
                <SkeletonItem size={32} shape="rectangle" className={classes.skeletonItem}/>
            </div>
            <div className={classes.userContainer}>
                <SkeletonItem size={32} shape="rectangle" className={classes.skeletonItem}/>
            </div>
            <div className={classes.assistantContainer}>
                <SkeletonItem size={32} shape="rectangle" className={classes.skeletonItem}/>
            </div>
        </Skeleton>
    );
};