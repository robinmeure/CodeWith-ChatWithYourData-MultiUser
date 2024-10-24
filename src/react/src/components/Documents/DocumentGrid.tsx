import { Button, Table, TableBody, TableCell, TableCellLayout, TableHeader, TableHeaderCell, TableRow, makeStyles, tokens } from '@fluentui/react-components';
import { Delete12Regular } from '@fluentui/react-icons/fonts';
import { IDocument } from '../../models/Document';

const useClasses = makeStyles({
    container: {
        display: 'flex',
        marginTop: tokens.spacingVerticalM,
    },
    deleteColumn: {
        display: 'flex',
        justifyContent: 'flex-end'
    },
    headerText: {
        fontWeight: tokens.fontWeightSemibold,
    }
});

const columns = [
    { columnKey: "fileName", label: "Document name" },
    { columnKey: "status", label: "Status" },
    { columnKey: "lastUpdated", label: "Last updated" }
];

type documentGridProps = { 
      documents?: IDocument[];
      deleteDocument: (documentId: string) => Promise<boolean>
}

export function DocumentGrid({ documents, deleteDocument } : documentGridProps) {

    const classes = useClasses();

    return (
        <div className={classes.container}>
            <Table
                role="grid"
                aria-label="File table"
            >
                <TableHeader>
                    <TableRow>
                        {columns.map((column) => (
                            <TableHeaderCell key={column.columnKey} className={classes.headerText}>
                                {column.label}
                            </TableHeaderCell>
                        ))}
                        <TableHeaderCell />
                    </TableRow>
                </TableHeader>

                <TableBody>
                    {documents && documents.map((item) => (
                        <TableRow key={item.id}>
                            <TableCell tabIndex={0} role="gridcell">
                                {item.documentName}
                            </TableCell>
                            <TableCell tabIndex={0} role="gridcell">
                                {item.availableInSearchIndex}
                            </TableCell>
                            <TableCell tabIndex={0} role="gridcell">
                                {item.lastUpdated}
                            </TableCell>
                            <TableCell role="gridcell">
                                <TableCellLayout className={classes.deleteColumn}>
                                    <Button icon={<Delete12Regular />} onClick={() => deleteDocument(item.id)}/>
                                </TableCellLayout>
                            </TableCell>
                        </TableRow>
                    ))}
                </TableBody>
            </Table>
        </div>
    );
};